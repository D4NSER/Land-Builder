using LandBuilder.Application;
using LandBuilder.Domain;
using Xunit;

namespace LandBuilder.Tests;

public sealed class GameRulesTests
{
    private static int Slot(int q, int r) => HexGrid.Encode(new HexCoord(q, r));

    /// <summary>A state with a hand-picked board and tile in hand, no quests and a healthy stack.</summary>
    private static GameState Board(TileSpec hand, params (int Slot, TileSpec Tile, int Rotation)[] tiles)
    {
        var state = GameState.CreateInitial(42);
        state.Board.Clear();
        foreach (var (slot, tile, rotation) in tiles)
            state.Board[slot] = new PlacedTile(tile, rotation, 0);
        state.CurrentTile = hand;
        return state;
    }

    [Fact]
    public void EdgeTerrain_RotationShiftsSectorsClockwise()
    {
        var tile = new TileSpec(Terrain.Forest, Terrain.Grass, Terrain.Grass, Terrain.Grass, Terrain.Grass, Terrain.Grass);

        Assert.Equal(Terrain.Forest, tile.EdgeTerrain(0, 0));
        Assert.Equal(Terrain.Forest, tile.EdgeTerrain(1, 1));
        Assert.Equal(Terrain.Forest, tile.EdgeTerrain(5, 5));
        Assert.Equal(Terrain.Grass, tile.EdgeTerrain(0, 1));
    }

    [Fact]
    public void HexGrid_NeighborAndOppositeEdgesAgree()
    {
        var origin = HexGrid.OriginSlotIndex;
        for (var edge = 0; edge < 6; edge++)
        {
            var neighbor = HexGrid.NeighborSlot(origin, edge);
            Assert.Equal(edge, HexGrid.EdgeFacing(origin, neighbor));
            Assert.Equal(HexGrid.OppositeEdge(edge), HexGrid.EdgeFacing(neighbor, origin));
        }
    }

    [Fact]
    public void Evaluate_CountsMatchedAndMismatchedEdges()
    {
        var state = Board(
            TileSpec.Pure(Terrain.Forest),
            (Slot(0, 0), TileSpec.Pure(Terrain.Forest), 0),
            (Slot(2, -1), TileSpec.Pure(Terrain.Water), 0));

        // (1,0) touches origin through its edge 3 and (2,-1) through its edge 1.
        var eval = GameEngine.Evaluate(state, Slot(1, 0), 0);

        Assert.True(eval.IsValid);
        Assert.Equal([3], eval.MatchedEdges);
        Assert.Equal([1], eval.MismatchedEdges);
        Assert.Equal(GameRules.PointsPerMatchedEdge, eval.TotalPoints);
        Assert.Equal(PlacementQuality.Normal, eval.Quality);
        Assert.True(eval.BreaksStreak);
        Assert.False(eval.BuildsStreak);
    }

    [Fact]
    public void Evaluate_RejectsDisconnectedAndOccupiedSlots()
    {
        var state = Board(TileSpec.Pure(Terrain.Grass), (Slot(0, 0), TileSpec.Pure(Terrain.Grass), 0));

        Assert.Equal(PlacementRejectionReason.NotConnected, GameEngine.Evaluate(state, Slot(5, 5), 0).RejectionReason);
        Assert.Equal(PlacementRejectionReason.SlotOccupied, GameEngine.Evaluate(state, Slot(0, 0), 0).RejectionReason);

        state.CurrentTile = null;
        Assert.Equal(PlacementRejectionReason.NoTileInHand, GameEngine.Evaluate(state, Slot(1, 0), 0).RejectionReason);
    }

    [Fact]
    public void Place_RejectionLeavesStateUntouched()
    {
        var state = GameState.CreateInitial(7);
        var before = state.Fingerprint();

        var (next, events) = GameEngine.Place(state, Slot(9, 9), 0);

        Assert.Same(state, next);
        Assert.Equal(before, next.Fingerprint());
        Assert.IsType<PlacementRejectedEvent>(Assert.Single(events));
    }

    [Fact]
    public void Perfect_RequiresThreeMatchingNeighbours_AndAwardsATile()
    {
        var grass = TileSpec.Pure(Terrain.Grass);
        // Target (0,0); neighbours on edges 0,1,2.
        var state = Board(grass,
            (Slot(1, 0), grass, 0),
            (Slot(1, -1), grass, 0),
            (Slot(0, -1), grass, 0));
        var stack = state.StackCount;

        var eval = GameEngine.Evaluate(state, Slot(0, 0), 0);
        Assert.Equal(PlacementQuality.Perfect, eval.Quality);
        Assert.Equal(3 * GameRules.PerfectBonusPerNeighbor, eval.BonusPoints);

        var (next, events) = GameEngine.Place(state, Slot(0, 0), 0);
        Assert.Contains(events, e => e is TilesAwardedEvent { Reason: "Perfect" });
        // One tile moved into hand, one tile awarded: stack size unchanged.
        Assert.Equal(stack - 1 + GameRules.PerfectTileReward, next.StackCount);
        Assert.Equal(1, next.Stats.PerfectPlacements);
    }

    [Fact]
    public void Flawless_WhenAllSixNeighboursMatch()
    {
        var grass = TileSpec.Pure(Terrain.Grass);
        var tiles = Enumerable.Range(0, 6)
            .Select(e => (HexGrid.NeighborSlot(HexGrid.OriginSlotIndex, e), grass, 0))
            .ToArray();
        var state = Board(grass, tiles);

        var eval = GameEngine.Evaluate(state, HexGrid.OriginSlotIndex, 0);

        Assert.Equal(PlacementQuality.Flawless, eval.Quality);
        Assert.Equal(GameRules.FlawlessBonus, eval.BonusPoints);
        Assert.Equal(GameRules.FlawlessTileReward, eval.TilesAwarded);
    }

    [Fact]
    public void Streak_GrowsOnCleanPlacements_AndResetsOnMismatch()
    {
        var grass = TileSpec.Pure(Terrain.Grass);
        var state = Board(grass, (Slot(0, 0), grass, 0), (Slot(1, -1), grass, 0));

        // Single clean match holds the streak; two or more grow it.
        var single = GameEngine.Evaluate(state, Slot(-1, 0), 0);
        Assert.False(single.BuildsStreak);
        Assert.False(single.BreaksStreak);

        (state, _) = GameEngine.Place(state, Slot(1, 0), 0);
        Assert.Equal(1, state.Streak);
        Assert.Equal(1.1, state.StreakMultiplier, 3);

        state.CurrentTile = grass;
        var boosted = GameEngine.Evaluate(state, Slot(-1, 0), 0);
        Assert.Equal(11, boosted.EdgePoints);

        state.CurrentTile = TileSpec.Pure(Terrain.Water);
        (state, var events) = GameEngine.Place(state, Slot(-1, 0), 0);
        Assert.Equal(0, state.Streak);
        Assert.Contains(events, e => e is StreakChangedEvent { Current: 0 });
        Assert.Equal(1, state.Stats.BestStreak);
    }

    [Fact]
    public void RegionMap_JoinsMatchingEdgesAcrossTiles_AndDetectsClosure()
    {
        var forest = TileSpec.Pure(Terrain.Forest);
        var board = new Dictionary<int, PlacedTile>
        {
            [Slot(0, 0)] = new(forest, 0, 0),
            [Slot(1, 0)] = new(forest, 0, 0),
            [Slot(5, 5)] = new(TileSpec.Pure(Terrain.Water), 0, 0)
        };

        var map = RegionMap.Build(board);
        var forestRegion = map.RegionAt(new SectorRef(Slot(0, 0), 0))!;

        Assert.Equal(12, forestRegion.Size);
        Assert.Equal(10, forestRegion.OpenEdges);
        Assert.False(forestRegion.IsClosed);
        Assert.Same(forestRegion, map.RegionAt(new SectorRef(Slot(1, 0), 4)));
        Assert.Equal(2, map.Regions.Count);

        // A pond fully ringed by grass is closed.
        var pond = new Dictionary<int, PlacedTile> { [Slot(0, 0)] = new(new TileSpec(Terrain.Water, Terrain.Grass, Terrain.Grass, Terrain.Grass, Terrain.Grass, Terrain.Grass), 0, 0) };
        pond[Slot(1, 0)] = new(TileSpec.Pure(Terrain.Grass), 0, 0);
        var water = RegionMap.Build(pond).RegionAt(new SectorRef(Slot(0, 0), 0))!;
        Assert.Equal(Terrain.Water, water.Terrain);
        Assert.Equal(1, water.Size);
        Assert.True(water.IsClosed);
    }

    [Fact]
    public void Quest_CompletesWhenRegionReachesTarget()
    {
        var forest = TileSpec.Pure(Terrain.Forest);
        var state = Board(forest, (Slot(0, 0), forest, 0));
        state.Quests.Add(new Quest(1, Terrain.Forest, QuestKind.AtLeast, 12, new SectorRef(Slot(0, 0), 0), 4, 60, 0));
        var stack = state.StackCount;
        var score = state.Score;

        var (next, events) = GameEngine.Place(state, Slot(1, 0), 0);

        var completed = Assert.Single(events.OfType<QuestCompletedEvent>());
        Assert.Equal(12, completed.FinalSize);
        Assert.DoesNotContain(next.Quests, q => q.Id == 1);
        Assert.Equal(score + 10 + 60, next.Score);
        Assert.Equal(stack - 1 + 4, next.StackCount);
        Assert.Equal(1, next.Stats.QuestsCompleted);
    }

    [Fact]
    public void ExactQuest_FailsWhenOvershot()
    {
        var forest = TileSpec.Pure(Terrain.Forest);
        var state = Board(forest, (Slot(0, 0), forest, 0));
        state.Quests.Add(new Quest(1, Terrain.Forest, QuestKind.Exactly, 9, new SectorRef(Slot(0, 0), 0), 4, 60, 0));

        var (next, events) = GameEngine.Place(state, Slot(1, 0), 0);

        var failed = Assert.Single(events.OfType<QuestFailedEvent>());
        Assert.Equal(QuestFailReason.Overshot, failed.Reason);
        Assert.DoesNotContain(next.Quests, q => q.Id == 1);
    }

    [Fact]
    public void Quest_FailsWhenRegionGetsClosed()
    {
        var pond = new TileSpec(Terrain.Water, Terrain.Grass, Terrain.Grass, Terrain.Grass, Terrain.Grass, Terrain.Grass);
        var state = Board(TileSpec.Pure(Terrain.Grass), (Slot(0, 0), pond, 0));
        state.Quests.Add(new Quest(1, Terrain.Water, QuestKind.AtLeast, 10, new SectorRef(Slot(0, 0), 0), 4, 60, 0));

        var (_, events) = GameEngine.Place(state, Slot(1, 0), 0);

        Assert.Equal(QuestFailReason.RegionClosed, Assert.Single(events.OfType<QuestFailedEvent>()).Reason);
    }

    [Fact]
    public void Milestone_UnlocksMountainsAndAwardsTiles()
    {
        var grass = TileSpec.Pure(Terrain.Grass);
        var state = Board(grass, (Slot(0, 0), grass, 0));
        state.Score = GameRules.MilestoneThreshold(0) - 5;
        Assert.DoesNotContain(Terrain.Mountain, state.Unlocked);

        var (next, events) = GameEngine.Place(state, Slot(1, 0), 0);

        var milestone = Assert.Single(events.OfType<MilestoneReachedEvent>());
        Assert.Equal(Terrain.Mountain, milestone.UnlockedTerrain);
        Assert.Contains(Terrain.Mountain, next.Unlocked);
        Assert.Equal(1, next.MilestonesReached);
    }

    [Fact]
    public void MilestoneThresholds_StrictlyIncreaseWithWideningGaps()
    {
        long previousGap = 0;
        for (var i = 1; i < 20; i++)
        {
            var gap = GameRules.MilestoneThreshold(i) - GameRules.MilestoneThreshold(i - 1);
            Assert.True(gap > previousGap);
            previousGap = gap;
        }
    }

    [Fact]
    public void GameOver_WhenStackRunsDry()
    {
        var grass = TileSpec.Pure(Terrain.Grass);
        var state = Board(TileSpec.Pure(Terrain.Water), (Slot(0, 0), grass, 0));
        state.StackCount = 0;
        state.NextTile = null;

        var (next, events) = GameEngine.Place(state, Slot(1, 0), 0);

        Assert.True(next.IsGameOver);
        Assert.Null(next.CurrentTile);
        Assert.Single(events.OfType<GameOverEvent>());
        Assert.Equal(PlacementRejectionReason.GameOver, GameEngine.Evaluate(next, Slot(-1, 0), 0).RejectionReason);
    }

    [Fact]
    public void TileGenerator_OnlyUsesUnlockedTerrains_AndIsDeterministic()
    {
        var unlocked = new[] { Terrain.Grass, Terrain.Water };
        var a = new Rng(99);
        var b = new Rng(99);
        for (var i = 0; i < 500; i++)
        {
            var tile = TileGenerator.Generate(ref a, unlocked, i);
            Assert.Equal(tile, TileGenerator.Generate(ref b, unlocked, i));
            Assert.All(tile.ToArray(), t => Assert.Contains(t, unlocked));
        }
    }

    [Fact]
    public void Session_UndoRestoresPreviousStateAndSpendsAnUndo()
    {
        var session = GameSession.StartNew(5, new InMemoryEventSink());
        var before = session.State.Fingerprint();
        var best = GameEngine.FindBestPlacement(session.State)!;

        session.Place(best.Slot, best.Rotation);
        Assert.True(session.TryUndo(out var undone));

        Assert.Equal(best.Slot, undone);
        Assert.Equal(GameRules.UndosPerRun - 1, session.State.UndosRemaining);
        var restored = session.State.Clone();
        restored.UndosRemaining = GameRules.UndosPerRun;
        Assert.Equal(before, restored.Fingerprint());
        Assert.False(session.TryUndo(out _));
    }
}
