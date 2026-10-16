namespace LandBuilder.Domain;

public sealed record QuestStatus(Quest Quest, int CurrentSize, Region? Region);

/// <summary>
/// Pure rules: given a state and a placement, produce the next state plus the events that explain what happened.
/// Inputs are never mutated, so callers can keep old states around for undo and replays.
/// </summary>
public static class GameEngine
{
    private const int HintShortlistSize = 10;

    /// <param name="tileOverride">Evaluate a different tile than the one in hand (wonder cards).</param>
    public static PlacementEvaluation Evaluate(GameState state, int slot, int rotation, TileSpec? tileOverride = null)
    {
        rotation = NormalizeRotation(rotation);

        PlacementEvaluation Rejected(PlacementRejectionReason reason) =>
            new(slot, rotation, false, reason, [], [], 0, 0, state.StreakMultiplier, 0, PlacementQuality.Normal, 0, 0);

        if (state.IsGameOver)
            return Rejected(PlacementRejectionReason.GameOver);
        if ((tileOverride ?? state.CurrentTile) is not TileSpec tile)
            return Rejected(PlacementRejectionReason.NoTileInHand);
        if (state.Board.ContainsKey(slot))
            return Rejected(PlacementRejectionReason.SlotOccupied);

        var matched = new List<int>();
        var mismatched = new List<int>();
        for (var edge = 0; edge < 6; edge++)
        {
            if (!state.Board.TryGetValue(HexGrid.NeighborSlot(slot, edge), out var neighbor))
                continue;

            var mine = tile.EdgeTerrain(edge, rotation);
            var theirs = neighbor.EdgeTerrain(HexGrid.OppositeEdge(edge));
            if (!Terrains.IsLegalContact(mine, theirs))
                return Rejected(PlacementRejectionReason.RiverBlocked);
            (Terrains.Matches(mine, theirs) ? matched : mismatched).Add(edge);
        }

        var neighbors = matched.Count + mismatched.Count;
        if (neighbors == 0)
            return Rejected(PlacementRejectionReason.NotConnected);

        var multiplier = state.StreakMultiplier;
        var basePoints = matched.Count * GameRules.PointsPerMatchedEdge;
        var edgePoints = (int)Math.Round(basePoints * multiplier, MidpointRounding.AwayFromZero);

        var quality = PlacementQuality.Normal;
        var bonus = 0;
        var tiles = 0;
        if (mismatched.Count == 0 && neighbors == 6)
        {
            quality = PlacementQuality.Flawless;
            bonus = GameRules.FlawlessBonus;
            tiles = GameRules.FlawlessTileReward;
        }
        else if (mismatched.Count == 0 && neighbors >= GameRules.PerfectMinNeighbors)
        {
            quality = PlacementQuality.Perfect;
            bonus = GameRules.PerfectBonusPerNeighbor * neighbors;
            tiles = GameRules.PerfectTileReward;
        }

        return new PlacementEvaluation(slot, rotation, true, null, matched, mismatched, neighbors, basePoints, multiplier, edgePoints, quality, bonus, tiles);
    }

    public static (GameState State, IReadOnlyList<IDomainEvent> Events) Place(GameState state, int slot, int rotation) =>
        PlaceInternal(state, slot, rotation, wonderCard: null);

    /// <summary>Places the wonder card at <paramref name="cardIndex"/>. It does not use the stack or touch the streak.</summary>
    public static (GameState State, IReadOnlyList<IDomainEvent> Events) PlaceWonder(GameState state, int cardIndex, int slot, int rotation) =>
        PlaceInternal(state, slot, rotation, cardIndex);

    public static PlacementEvaluation EvaluateWonder(GameState state, int cardIndex, int slot, int rotation)
    {
        if (cardIndex < 0 || cardIndex >= state.WonderCards.Count)
            return new PlacementEvaluation(slot, NormalizeRotation(rotation), false, PlacementRejectionReason.NoWonderCard, [], [], 0, 0, state.StreakMultiplier, 0, PlacementQuality.Normal, 0, 0);
        return Evaluate(state, slot, rotation, Wonders.Info(state.WonderCards[cardIndex]).Edges);
    }

    private static (GameState State, IReadOnlyList<IDomainEvent> Events) PlaceInternal(GameState state, int slot, int rotation, int? wonderCard)
    {
        var events = new List<IDomainEvent>();
        var eval = wonderCard is int card ? EvaluateWonder(state, card, slot, rotation) : Evaluate(state, slot, rotation);
        if (!eval.IsValid)
        {
            events.Add(new PlacementRejectedEvent(slot, eval.RejectionReason!.Value, RejectionMessage(eval.RejectionReason.Value)));
            return (state, events);
        }

        var next = state.Clone();
        var rng = new Rng { State = next.RngState };
        var isWonder = wonderCard is not null;
        var turn = isWonder ? next.Turn : next.Turn + 1;
        var stats = next.Stats;

        if (wonderCard is int cardSlot)
        {
            var type = next.WonderCards[cardSlot];
            next.WonderCards.RemoveAt(cardSlot);
            next.Board[slot] = new PlacedTile(Wonders.Info(type).Edges, eval.Rotation, turn, type);
            next.Score += eval.TotalPoints;
            events.Add(new WonderPlacedEvent(slot, type, eval));
        }
        else
        {
            var tile = next.CurrentTile!.Value;
            next.Board[slot] = new PlacedTile(tile, eval.Rotation, turn);
            next.CurrentTile = null;
            next.Score += eval.TotalPoints;
            events.Add(new TilePlacedEvent(slot, tile, eval.Rotation, eval));

            stats = stats with
            {
                TilesPlaced = turn,
                PerfectPlacements = stats.PerfectPlacements + (eval.Quality == PlacementQuality.Perfect ? 1 : 0),
                FlawlessPlacements = stats.FlawlessPlacements + (eval.Quality == PlacementQuality.Flawless ? 1 : 0)
            };

            var previousStreak = next.Streak;
            next.Streak = eval.BreaksStreak ? 0 : eval.BuildsStreak ? next.Streak + 1 : next.Streak;
            stats = stats with { BestStreak = Math.Max(stats.BestStreak, next.Streak) };
            if (next.Streak != previousStreak)
                events.Add(new StreakChangedEvent(previousStreak, next.Streak, next.StreakMultiplier));
        }

        var tilesAwarded = 0;
        if (eval.TilesAwarded > 0)
        {
            tilesAwarded += eval.TilesAwarded;
            events.Add(new TilesAwardedEvent(eval.TilesAwarded, eval.Quality == PlacementQuality.Flawless ? "Flawless" : "Perfect"));
        }

        // Quests: resolve existing ones against the new region layout, then maybe offer a new one.
        var regions = RegionMap.Build(next.Board);
        var remaining = new List<Quest>();
        foreach (var quest in next.Quests)
        {
            var region = regions.RegionAt(quest.Anchor);
            var size = region?.Size ?? 0;
            var done = quest.Kind == QuestKind.AtLeast ? size >= quest.Target : size == quest.Target;
            if (done)
            {
                next.Score += quest.RewardPoints;
                tilesAwarded += quest.RewardTiles;
                stats = stats with { QuestsCompleted = stats.QuestsCompleted + 1 };
                events.Add(new QuestCompletedEvent(quest, size));
                events.Add(new TilesAwardedEvent(quest.RewardTiles, "Quest"));
                if (stats.QuestsCompleted % GameRules.QuestsPerWonder == 0)
                    AwardWonder(next, ref rng, events, "Quests");
                continue;
            }

            if (quest.Kind == QuestKind.Exactly && size > quest.Target)
            {
                stats = stats with { QuestsFailed = stats.QuestsFailed + 1 };
                events.Add(new QuestFailedEvent(quest, QuestFailReason.Overshot, size));
                continue;
            }

            if (region is { IsClosed: true })
            {
                stats = stats with { QuestsFailed = stats.QuestsFailed + 1 };
                events.Add(new QuestFailedEvent(quest, QuestFailReason.RegionClosed, size));
                continue;
            }

            remaining.Add(quest);
        }

        next.Quests = remaining;
        TrySpawnQuest(next, regions, slot, turn, ref rng, events);

        foreach (var region in regions.Regions)
        {
            if (region.Terrain != Terrain.Grass && region.Size > stats.LargestRegion)
                stats = stats with { LargestRegion = region.Size, LargestRegionTerrain = region.Terrain };
        }

        // Wonders finish once their neighbours satisfy the recipe; any placement can complete one.
        foreach (var wonderSlot in next.Board.Where(kv => kv.Value.Wonder is not null && !kv.Value.Built).Select(kv => kv.Key).ToList())
        {
            var progress = Wonders.Progress(next, wonderSlot);
            if (!progress.Complete)
                continue;
            next.Board[wonderSlot] = next.Board[wonderSlot] with { Built = true };
            next.Score += progress.Def.RewardPoints;
            tilesAwarded += progress.Def.RewardTiles;
            stats = stats with { WondersBuilt = stats.WondersBuilt + 1 };
            events.Add(new WonderCompletedEvent(wonderSlot, progress.Def.Type, progress.Def.RewardPoints, progress.Def.RewardTiles));
            events.Add(new TilesAwardedEvent(progress.Def.RewardTiles, "Wonder"));
        }

        // Milestones may chain if a big quest pushes the score far enough.
        while (next.Score >= next.NextMilestoneScore)
        {
            var index = next.MilestonesReached;
            var threshold = next.NextMilestoneScore;
            next.MilestonesReached++;
            tilesAwarded += GameRules.MilestoneTileReward;
            var unlock = GameRules.MilestoneUnlock(next.Theme, index);
            if (unlock is Terrain t && !next.Unlocked.Contains(t))
                next.Unlocked.Add(t);
            else
                unlock = null;
            events.Add(new MilestoneReachedEvent(index, threshold, GameRules.MilestoneTileReward, unlock));
            events.Add(new TilesAwardedEvent(GameRules.MilestoneTileReward, "Milestone"));
            if (GameRules.MilestoneAwardsWonder(index))
                AwardWonder(next, ref rng, events, "Milestone");
        }

        next.Stats = stats;
        next.StackCount += tilesAwarded;
        if (isWonder)
        {
            // The hand stays as it was; just make sure a freshly earned stack shows its next tile.
            if (next.StackCount > 0 && next.NextTile is null)
                next.NextTile = TileGenerator.Generate(ref rng, next.Unlocked, turn + 1, next.Quests.Select(q => q.Terrain).Distinct().ToArray(), next.Theme);
        }
        else
        {
            AdvanceHand(next, ref rng, turn);
        }

        // Never strand the player: a tile whose rivers cannot connect anywhere is set aside.
        while (next.CurrentTile is TileSpec stuck && !HasAnyValidPlacement(next))
        {
            events.Add(new TileDiscardedEvent(stuck));
            AdvanceHand(next, ref rng, turn);
        }

        next.RngState = rng.State;
        events.Add(new HandAdvancedEvent(next.CurrentTile, next.NextTile, next.StackCount));

        if (next.CurrentTile is null)
        {
            next.IsGameOver = true;
            events.Add(new GameOverEvent(next.Score, next.Stats));
        }

        return (next, events);
    }

    public static IReadOnlyList<QuestStatus> GetQuestStatuses(GameState state)
    {
        if (state.Quests.Count == 0)
            return [];

        var regions = RegionMap.Build(state.Board);
        return state.Quests
            .Select(q =>
            {
                var region = regions.RegionAt(q.Anchor);
                return new QuestStatus(q, region?.Size ?? 0, region);
            })
            .ToArray();
    }

    /// <summary>
    /// Best move for the tile in hand, judged by actually playing it out: points, tiles won (perfects, quests,
    /// milestones) and streak health. Used for the hint button and the attract-mode bot.
    /// </summary>
    public static PlacementEvaluation? FindBestPlacement(GameState state)
    {
        if (state.CurrentTile is null || state.IsGameOver)
            return null;

        // Cheap pass over every legal move, then play out only the most promising ones.
        var shortlist = new List<(PlacementEvaluation Eval, double Quick)>();
        foreach (var slot in state.CandidateSlots())
        {
            for (var rotation = 0; rotation < 6; rotation++)
            {
                var eval = Evaluate(state, slot, rotation);
                if (!eval.IsValid)
                    continue;
                var quick = eval.TotalPoints + (eval.TilesAwarded * 45.0) - (eval.MismatchedEdges.Count * 10) + (eval.NeighborCount * 3);
                shortlist.Add((eval, quick));
            }
        }

        if (shortlist.Count == 0)
            return null;

        var questStatuses = GetQuestStatuses(state);
        PlacementEvaluation? best = null;
        var bestScore = double.MinValue;
        foreach (var (eval, _) in shortlist.OrderByDescending(x => x.Quick).ThenBy(x => x.Eval.Slot).ThenBy(x => x.Eval.Rotation).Take(HintShortlistSize))
        {
            var (next, _) = Place(state, eval.Slot, eval.Rotation);
            var score = (next.Score - state.Score)
                        + ((next.TilesLeft - state.TilesLeft) * 45.0)
                        + (eval.BuildsStreak ? 12 : 0)
                        - (eval.BreaksStreak ? 8 + (state.Streak * 4) : 0)
                        - (eval.MismatchedEdges.Count * 10)
                        + (eval.NeighborCount * 3)
                        + QuestProgressValue(questStatuses, next);
            if (score > bestScore)
            {
                bestScore = score;
                best = eval;
            }
        }

        return best;
    }

    /// <summary>Rewards moves that grow quest regions without overshooting exact targets.</summary>
    private static double QuestProgressValue(IReadOnlyList<QuestStatus> before, GameState after)
    {
        if (before.Count == 0)
            return 0;

        var value = 0.0;
        var regions = RegionMap.Build(after.Board);
        foreach (var status in before)
        {
            if (!after.Quests.Any(q => q.Id == status.Quest.Id))
                continue;
            var size = regions.RegionAt(status.Quest.Anchor)?.Size ?? 0;
            var gained = Math.Min(size, status.Quest.Target) - status.CurrentSize;
            value += gained * 6.0;
        }

        return value;
    }

    private static void AwardWonder(GameState state, ref Rng rng, List<IDomainEvent> events, string reason)
    {
        if (state.WonderCards.Count >= Wonders.MaxCards)
            return;
        if (Wonders.Draw(ref rng, state) is WonderType type)
        {
            state.WonderCards.Add(type);
            events.Add(new WonderAwardedEvent(type, reason));
        }
    }

    /// <summary>
    /// Where the bot (and the hint) would put a wonder card: legal spots that already satisfy as much of the recipe
    /// as possible, leave room to finish it, and match edges.
    /// </summary>
    public static PlacementEvaluation? FindBestWonderPlacement(GameState state, int cardIndex)
    {
        if (cardIndex < 0 || cardIndex >= state.WonderCards.Count || state.IsGameOver)
            return null;

        var def = Wonders.Info(state.WonderCards[cardIndex]);
        PlacementEvaluation? best = null;
        var bestScore = double.MinValue;
        foreach (var slot in state.CandidateSlots())
        {
            for (var rotation = 0; rotation < 6; rotation++)
            {
                var eval = EvaluateWonder(state, cardIndex, slot, rotation);
                if (!eval.IsValid)
                    continue;

                var have = new Dictionary<Terrain, int>();
                var empty = 0;
                for (var edge = 0; edge < 6; edge++)
                {
                    if (state.Board.TryGetValue(HexGrid.NeighborSlot(slot, edge), out var neighbor))
                    {
                        var t = neighbor.EdgeTerrain(HexGrid.OppositeEdge(edge));
                        have[t] = have.GetValueOrDefault(t) + 1;
                    }
                    else
                    {
                        empty++;
                    }
                }

                var satisfied = def.Recipe.Sum(r => Math.Min(have.GetValueOrDefault(r.Key), r.Value));
                var missing = def.Recipe.Sum(r => Math.Max(0, r.Value - have.GetValueOrDefault(r.Key)));
                var score = (satisfied * 30.0) + eval.TotalPoints + (missing <= empty ? 25 : -60);
                if (score > bestScore)
                {
                    bestScore = score;
                    best = eval;
                }
            }
        }

        return best;
    }

    public static bool HasAnyValidPlacement(GameState state)
    {
        if (state.CurrentTile is null)
            return false;
        foreach (var slot in state.CandidateSlots())
        {
            for (var rotation = 0; rotation < 6; rotation++)
            {
                if (Evaluate(state, slot, rotation).IsValid)
                    return true;
            }
        }

        return false;
    }

    public static int NormalizeRotation(int rotation) => ((rotation % 6) + 6) % 6;

    public static string RejectionMessage(PlacementRejectionReason reason) => reason switch
    {
        PlacementRejectionReason.GameOver => "The run is over.",
        PlacementRejectionReason.NoTileInHand => "No tile in hand.",
        PlacementRejectionReason.SlotOccupied => "That spot is already taken.",
        PlacementRejectionReason.NotConnected => "Tiles must touch your land.",
        PlacementRejectionReason.RiverBlocked => "Rivers have to connect to a river or a lake.",
        PlacementRejectionReason.NoWonderCard => "No wonder card to place.",
        _ => "Can't place there."
    };

    private static void TrySpawnQuest(GameState state, RegionMap regions, int placedSlot, int turn, ref Rng rng, List<IDomainEvent> events)
    {
        if (turn < GameRules.FirstQuestTurn || state.Quests.Count >= GameRules.MaxActiveQuests)
            return;

        // Always keep at least one quest going; otherwise roll.
        var roll = rng.NextDouble();
        if (state.Quests.Count > 0 && roll >= GameRules.QuestSpawnChance)
            return;

        var questedRegions = state.Quests
            .Select(q => regions.RegionAt(q.Anchor)?.Id)
            .Where(id => id is not null)
            .ToHashSet();

        var candidates = regions.RegionsTouchingSlot(placedSlot)
            .Where(r => Terrains.CanHostQuest(r.Terrain)
                        && !r.IsClosed
                        && r.Size >= GameRules.MinQuestRegionSize
                        && !questedRegions.Contains(r.Id))
            .Select(r => (r, (double)(Terrains.Info(r.Terrain).QuestWeight * Math.Sqrt(r.Size) * Math.Min(r.OpenEdges, 6))))
            .ToArray();
        if (candidates.Length == 0)
            return;

        var region = rng.PickWeighted(candidates);
        var kind = region.Size >= 4 && rng.Chance(0.22) ? QuestKind.Exactly : QuestKind.AtLeast;
        var growth = rng.NextInt(4, 10) + (turn / 12) + (region.Size / 5);
        if (kind == QuestKind.Exactly)
            growth = Math.Max(3, growth - 2);
        var target = Math.Min(region.Size + growth, 80);

        var rewardTiles = Math.Clamp(3 + (growth / 4), 3, 7) + (kind == QuestKind.Exactly ? 2 : 0);
        var rewardPoints = growth * (kind == QuestKind.Exactly ? 20 : 15);
        var anchor = region.Sectors.First(s => s.Slot == placedSlot);

        var quest = new Quest(state.NextQuestId++, region.Terrain, kind, target, anchor, rewardTiles, rewardPoints, turn);
        state.Quests.Add(quest);
        events.Add(new QuestStartedEvent(quest, region.Size));
    }

    private static void AdvanceHand(GameState state, ref Rng rng, int turn)
    {
        var questTerrains = state.Quests.Select(q => q.Terrain).Distinct().ToArray();

        if (state.StackCount > 0 && state.NextTile is null)
            state.NextTile = TileGenerator.Generate(ref rng, state.Unlocked, turn + 1, questTerrains, state.Theme);

        if (state.StackCount <= 0)
        {
            state.CurrentTile = null;
            state.NextTile = null;
            state.StackCount = 0;
            return;
        }

        state.CurrentTile = state.NextTile;
        state.StackCount -= 1;
        state.NextTile = state.StackCount > 0
            ? TileGenerator.Generate(ref rng, state.Unlocked, turn + 2, questTerrains, state.Theme)
            : null;
    }
}
