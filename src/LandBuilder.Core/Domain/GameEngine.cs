namespace LandBuilder.Domain;

public sealed record QuestStatus(Quest Quest, int CurrentSize, Region? Region);

/// <summary>
/// Pure rules: given a state and a placement, produce the next state plus the events that explain what happened.
/// Inputs are never mutated, so callers can keep old states around for undo and replays.
/// </summary>
public static class GameEngine
{
    private const int HintShortlistSize = 10;

    public static PlacementEvaluation Evaluate(GameState state, int slot, int rotation)
    {
        rotation = NormalizeRotation(rotation);

        PlacementEvaluation Rejected(PlacementRejectionReason reason) =>
            new(slot, rotation, false, reason, [], [], 0, 0, state.StreakMultiplier, 0, PlacementQuality.Normal, 0, 0);

        if (state.IsGameOver)
            return Rejected(PlacementRejectionReason.GameOver);
        if (state.CurrentTile is not TileSpec tile)
            return Rejected(PlacementRejectionReason.NoTileInHand);
        if (state.Board.ContainsKey(slot))
            return Rejected(PlacementRejectionReason.SlotOccupied);
        if (Wonders.PlotAt(state, slot) is { } plot && !Wonders.Suits(plot.Rule, tile))
            return Rejected(PlacementRejectionReason.WrongTileForPlot);

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

    public static (GameState State, IReadOnlyList<IDomainEvent> Events) Place(GameState state, int slot, int rotation)
    {
        var events = new List<IDomainEvent>();
        var eval = Evaluate(state, slot, rotation);
        if (!eval.IsValid)
        {
            events.Add(new PlacementRejectedEvent(slot, eval.RejectionReason!.Value, RejectionMessage(eval.RejectionReason.Value)));
            return (state, events);
        }

        var before = RegionMap.Build(state.Board);
        var next = state.Clone();
        var rng = new Rng { State = next.RngState };
        var turn = next.Turn + 1;
        var stats = next.Stats;

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

        var tilesAwarded = 0;
        if (eval.TilesAwarded > 0)
        {
            tilesAwarded += eval.TilesAwarded;
            events.Add(new TilesAwardedEvent(eval.TilesAwarded, eval.Quality == PlacementQuality.Flawless ? "Flawless" : "Perfect"));
        }

        var regions = RegionMap.Build(next.Board);
        ScoreConnections(before, regions, slot, next, ref stats, ref tilesAwarded, events);

        // Quests: resolve existing ones against the new region layout, then maybe offer a new one.
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

        if (Wonders.PlotAt(state, slot) is { } plot)
        {
            var progress = Wonders.Progress(next, plot.Site);
            events.Add(new PlotFilledEvent(plot.Site, slot, progress.Done, progress.Def.Size));
        }

        CompleteSites(next, ref stats, ref tilesAwarded, events);
        ReachMilestones(next, ref rng, ref tilesAwarded, events);

        next.Stats = stats;
        next.StackCount += tilesAwarded;
        AdvanceHand(next, ref rng, turn);
        return Finish(next, ref rng, turn, events);
    }

    /// <summary>Lays the blueprint on card <paramref name="cardIndex"/>. It does not use the stack or touch the streak.</summary>
    public static (GameState State, IReadOnlyList<IDomainEvent> Events) PlaceWonder(GameState state, int cardIndex, int anchor, int rotation)
    {
        var events = new List<IDomainEvent>();
        var plan = PlanWonder(state, cardIndex, anchor, rotation);
        if (!plan.IsValid)
        {
            events.Add(new PlacementRejectedEvent(anchor, plan.RejectionReason!.Value, RejectionMessage(plan.RejectionReason.Value)));
            return (state, events);
        }

        var next = state.Clone();
        var rng = new Rng { State = next.RngState };
        var stats = next.Stats;
        next.WonderCards.RemoveAt(cardIndex);
        var site = new WonderSite(next.NextSiteId++, plan.Type, anchor, plan.Rotation, next.Turn);
        next.Sites.Add(site);
        events.Add(new WonderPlacedEvent(site, plan));

        var tilesAwarded = 0;
        CompleteSites(next, ref stats, ref tilesAwarded, events);
        ReachMilestones(next, ref rng, ref tilesAwarded, events);
        next.Stats = stats;
        next.StackCount += tilesAwarded;

        // The hand stays as it was; just make sure a freshly earned stack shows its next tile.
        if (next.StackCount > 0 && next.NextTile is null)
            next.NextTile = TileGenerator.Generate(ref rng, next.Unlocked, next.Turn + 1, WantedTerrains(next), next.Theme);
        return Finish(next, ref rng, next.Turn, events);
    }

    public static SitePlan PlanWonder(GameState state, int cardIndex, int anchor, int rotation)
    {
        if (state.IsGameOver)
            return new SitePlan(WonderType.Castle, anchor, NormalizeRotation(rotation), false, PlacementRejectionReason.GameOver, []);
        if (cardIndex < 0 || cardIndex >= state.WonderCards.Count)
            return new SitePlan(WonderType.Castle, anchor, NormalizeRotation(rotation), false, PlacementRejectionReason.NoWonderCard, []);
        return Wonders.Plan(state, state.WonderCards[cardIndex], anchor, rotation);
    }

    private static (GameState State, IReadOnlyList<IDomainEvent> Events) Finish(GameState next, ref Rng rng, int turn, List<IDomainEvent> events)
    {
        // Never strand the player: a tile that fits nowhere (rivers, blueprint plots) is set aside.
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

    /// <summary>
    /// Pays for growing and closing the regions the new tile touches. A region's previous rung is the best rung any
    /// of the regions it swallowed had reached, so merging two Towns into a City pays only for the City.
    /// </summary>
    private static void ScoreConnections(RegionMap before, RegionMap after, int slot, GameState next, ref RunStats stats, ref int tilesAwarded, List<IDomainEvent> events)
    {
        foreach (var region in after.RegionsTouchingSlot(slot))
        {
            if (!RegionTiers.HasTiers(region.Terrain))
                continue;

            var previous = 0;
            var seen = new HashSet<int>();
            foreach (var sector in region.Sectors)
            {
                if (sector.Slot == slot || before.RegionAt(sector) is not Region old || !seen.Add(old.Id))
                    continue;
                previous = Math.Max(previous, RegionTiers.Level(region.Terrain, old.Size));
            }

            var level = RegionTiers.Level(region.Terrain, region.Size);
            List<int>? slots = null;
            for (var rung = previous + 1; rung <= level; rung++)
            {
                var tier = RegionTiers.Get(region.Terrain, rung);
                slots ??= region.Sectors.Select(s => s.Slot).Distinct().ToList();
                next.Score += tier.Points;
                tilesAwarded += tier.Tiles;
                stats = stats with { TiersReached = stats.TiersReached + 1 };
                events.Add(new RegionTierReachedEvent(tier, region.Size, slots, region.Sectors.First(s => s.Slot == slot)));
                events.Add(new TilesAwardedEvent(tier.Tiles, tier.Name));
            }

            if (region.Terrain == Terrain.Village)
                stats = stats with { BestVillage = Math.Max(stats.BestVillage, level) };

            if (!region.IsClosed)
                continue;
            var (points, tiles) = RegionTiers.ClosureReward(region.Terrain, region.Size);
            if (points <= 0)
                continue;
            slots ??= region.Sectors.Select(s => s.Slot).Distinct().ToList();
            next.Score += points;
            tilesAwarded += tiles;
            stats = stats with { RegionsClosed = stats.RegionsClosed + 1 };
            events.Add(new RegionClosedEvent(region.Terrain, region.Size, RegionTiers.NameFor(region.Terrain, region.Size), points, tiles, slots));
            if (tiles > 0)
                events.Add(new TilesAwardedEvent(tiles, "Completed"));
        }
    }

    private static void CompleteSites(GameState next, ref RunStats stats, ref int tilesAwarded, List<IDomainEvent> events)
    {
        for (var i = 0; i < next.Sites.Count; i++)
        {
            var site = next.Sites[i];
            if (site.Built || !Wonders.Progress(next, site).Complete)
                continue;
            var def = Wonders.Info(site.Type);
            site = site with { Built = true };
            next.Sites[i] = site;
            next.Score += def.RewardPoints;
            tilesAwarded += def.RewardTiles;
            stats = stats with { WondersBuilt = stats.WondersBuilt + 1 };
            events.Add(new WonderCompletedEvent(site, def.RewardPoints, def.RewardTiles));
            events.Add(new TilesAwardedEvent(def.RewardTiles, "Wonder"));
        }
    }

    /// <summary>Milestones may chain if a big reward pushes the score far enough.</summary>
    private static void ReachMilestones(GameState next, ref Rng rng, ref int tilesAwarded, List<IDomainEvent> events)
    {
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
    /// milestones, region rungs) and streak health. Used for the hint button and the attract-mode bot.
    /// </summary>
    public static PlacementEvaluation? FindBestPlacement(GameState state)
    {
        if (state.CurrentTile is null || state.IsGameOver)
            return null;

        // Cheap pass over every legal move, then play out only the most promising ones.
        var shortlist = new List<(PlacementEvaluation Eval, double Quick)>();
        foreach (var slot in state.CandidateSlots())
        {
            var plot = Wonders.PlotAt(state, slot) is not null ? 40.0 : 0.0;
            for (var rotation = 0; rotation < 6; rotation++)
            {
                var eval = Evaluate(state, slot, rotation);
                if (!eval.IsValid)
                    continue;
                var quick = eval.TotalPoints + (eval.TilesAwarded * 45.0) - (eval.MismatchedEdges.Count * 10) + (eval.NeighborCount * 3) + plot;
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
                        + (Wonders.PlotAt(state, eval.Slot) is not null ? 40 : 0)
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
    /// Where the bot (and the hint) would lay a blueprint: over tiles that already suit it where possible, with the
    /// remaining plots next to the island so they can be filled soon. A spot that completes at once wins outright.
    /// </summary>
    public static SitePlan? FindBestSite(GameState state, int cardIndex)
    {
        if (cardIndex < 0 || cardIndex >= state.WonderCards.Count || state.IsGameOver)
            return null;

        var type = state.WonderCards[cardIndex];
        var anchors = new SortedSet<int>(state.Board.Keys);
        foreach (var slot in state.CandidateSlots())
        {
            anchors.Add(slot);
            foreach (var neighbor in HexGrid.GetNeighborSlots(slot))
                anchors.Add(neighbor);
        }

        SitePlan? best = null;
        var bestScore = double.MinValue;
        foreach (var anchor in anchors)
        {
            for (var rotation = 0; rotation < 6; rotation++)
            {
                var plan = Wonders.Plan(state, type, anchor, rotation);
                if (!plan.IsValid)
                    continue;
                var open = 0;
                foreach (var plot in plan.Plots.Where(p => !p.Filled))
                    open += HexGrid.GetNeighborSlots(plot.Slot).Any(state.Board.ContainsKey) ? 1 : 0;
                var empty = plan.Plots.Count(p => !p.Filled);
                var score = (plan.Ready * 60.0) + (open * 12.0) - (empty * 20.0) + (plan.CompletesAtOnce ? 500 : 0);
                if (score > bestScore)
                {
                    bestScore = score;
                    best = plan;
                }
            }
        }

        return best;
    }

    /// <summary>Terrains the island is asking for: open quests and blueprint plots still waiting for a tile.</summary>
    public static Terrain[] WantedTerrains(GameState state)
    {
        var wanted = state.Quests.Select(q => q.Terrain).ToList();
        foreach (var site in state.Sites.Where(s => !s.Built))
            wanted.AddRange(Wonders.Progress(state, site).Plots.Where(p => !p.Filled).Select(p => p.Rule.Terrain));
        return wanted.Distinct().ToArray();
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
        PlacementRejectionReason.SiteBlocked => "The blueprint needs free plots, or tiles that already suit them.",
        PlacementRejectionReason.WrongTileForPlot => "This blueprint plot needs a different tile.",
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
        var questTerrains = WantedTerrains(state);

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
