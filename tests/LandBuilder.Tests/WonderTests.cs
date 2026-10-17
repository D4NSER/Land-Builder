using LandBuilder.Domain;
using LandBuilder.Infrastructure;
using Xunit;

namespace LandBuilder.Tests;

public sealed class WonderTests
{
    private static int Slot(int q, int r) => HexGrid.Encode(new HexCoord(q, r));

    private static GameState Board(params (int Slot, TileSpec Tile)[] tiles)
    {
        var state = GameState.CreateInitial(42, WorldTheme.Temperate);
        state.Board.Clear();
        foreach (var (slot, tile) in tiles)
            state.Board[slot] = new PlacedTile(tile, 0, 0);
        state.CurrentTile = TileSpec.Pure(Terrain.Grass);
        state.Unlocked = Enum.GetValues<Terrain>().ToList();
        return state;
    }

    [Fact]
    public void EveryWonder_CoversAtLeastThreeDistinctHexes_InEveryRotation()
    {
        foreach (var type in Wonders.All)
        {
            Assert.True(Wonders.Info(type).Size >= 3, $"{type} is too small");
            for (var rotation = 0; rotation < 6; rotation++)
            {
                var slots = Wonders.Footprint(type, Slot(0, 0), rotation);
                Assert.Equal(slots.Length, slots.Distinct().Count());
            }
        }

        Assert.Equal(5, Wonders.Info(WonderType.TallShip).Size);
        Assert.All(Wonders.Info(WonderType.TallShip).Cells, c => Assert.Equal(Terrain.Water, c.Terrain));
    }

    [Fact]
    public void LayingABlueprint_UsesTheCard_NotTheStack()
    {
        var state = Board((Slot(0, 0), TileSpec.Pure(Terrain.Village)));
        state.WonderCards.Add(WonderType.Cathedral);
        var hand = state.CurrentTile;
        var stack = state.StackCount;

        var (next, events) = GameEngine.PlaceWonder(state, 0, Slot(1, 0), 0);

        var placed = Assert.Single(events.OfType<WonderPlacedEvent>());
        Assert.Empty(next.WonderCards);
        Assert.Equal(hand, next.CurrentTile);
        Assert.Equal(stack, next.StackCount);
        Assert.Equal(WonderType.Cathedral, Assert.Single(next.Sites).Type);
        Assert.False(next.Sites[0].Built);
        Assert.Equal(0, next.Stats.TilesPlaced);
        Assert.Equal(3, placed.Plan.Plots.Count);
    }

    [Fact]
    public void Plots_OnlyAcceptTilesThatSuitThem()
    {
        var state = Board((Slot(0, 0), TileSpec.Pure(Terrain.Grass)));
        state.WonderCards.Add(WonderType.TallShip);
        (state, _) = GameEngine.PlaceWonder(state, 0, Slot(1, 0), 0);
        Assert.Single(state.Sites);

        state.CurrentTile = TileSpec.Pure(Terrain.Grass);
        Assert.Equal(PlacementRejectionReason.WrongTileForPlot, GameEngine.Evaluate(state, Slot(1, 0), 0).RejectionReason);

        // One water edge is enough for the ship.
        state.CurrentTile = new TileSpec(Terrain.Water, Terrain.Grass, Terrain.Grass, Terrain.Grass, Terrain.Grass, Terrain.Grass);
        var (next, events) = GameEngine.Place(state, Slot(1, 0), 3);
        Assert.Contains(events, e => e is PlotFilledEvent { Done: 1, Total: 5 });
        Assert.True(next.Board.ContainsKey(Slot(1, 0)));
    }

    [Fact]
    public void Blueprint_CompletesWhenTheLastPlotIsFilled_AndPaysOnce()
    {
        var village = TileSpec.Pure(Terrain.Village);
        var state = Board((Slot(0, 0), village));
        state.WonderCards.Add(WonderType.Cathedral);
        (state, _) = GameEngine.PlaceWonder(state, 0, Slot(0, 0), 0);
        var plots = Wonders.Footprint(state.Sites[0]);
        Assert.Contains(Slot(0, 0), plots);

        var score = 0L;
        IReadOnlyList<IDomainEvent> events = [];
        foreach (var plot in plots.Where(p => p != Slot(0, 0)))
        {
            state.CurrentTile = village;
            score = state.Score;
            (state, events) = GameEngine.Place(state, plot, 0);
        }

        var done = Assert.Single(events.OfType<WonderCompletedEvent>());
        Assert.Equal(WonderType.Cathedral, done.Site.Type);
        Assert.True(state.Sites[0].Built);
        Assert.Equal(1, state.Stats.WondersBuilt);
        Assert.True(state.Score >= score + done.Points);

        state.CurrentTile = village;
        (_, var later) = GameEngine.Place(state, Slot(-1, 0), 0);
        Assert.Empty(later.OfType<WonderCompletedEvent>());
    }

    [Fact]
    public void Blueprint_OverTilesThatAlreadySuit_IsBuiltAtOnce()
    {
        var mountain = TileSpec.Pure(Terrain.Mountain);
        var plots = Wonders.Footprint(WonderType.Observatory, Slot(0, 0), 0);
        var state = Board(plots.Select(p => (p, mountain)).ToArray());
        state.WonderCards.Add(WonderType.Observatory);

        Assert.True(GameEngine.PlanWonder(state, 0, Slot(0, 0), 0).CompletesAtOnce);
        var (next, events) = GameEngine.PlaceWonder(state, 0, Slot(0, 0), 0);

        Assert.Single(events.OfType<WonderCompletedEvent>());
        Assert.True(next.Sites[0].Built);
    }

    [Fact]
    public void Blueprint_CannotCoverUnsuitableTiles_OrAnotherWonder()
    {
        var state = Board((Slot(0, 0), TileSpec.Pure(Terrain.Grass)));
        state.WonderCards.Add(WonderType.Cathedral);
        state.WonderCards.Add(WonderType.Castle);

        Assert.Equal(PlacementRejectionReason.SiteBlocked, GameEngine.PlanWonder(state, 0, Slot(0, 0), 0).RejectionReason);
        Assert.Equal(PlacementRejectionReason.NotConnected, GameEngine.PlanWonder(state, 0, Slot(5, 5), 0).RejectionReason);

        (state, _) = GameEngine.PlaceWonder(state, 0, Slot(1, 0), 0);
        Assert.Equal(PlacementRejectionReason.SiteBlocked, GameEngine.PlanWonder(state, 0, Slot(1, 0), 0).RejectionReason);
    }

    [Fact]
    public void WaitingPlots_GetSuitableTilesDealt()
    {
        var state = Board((Slot(0, 0), TileSpec.Pure(Terrain.Grass)));
        state.Unlocked = Terrains.Starting.ToList();
        state.WonderCards.Add(WonderType.Cathedral);
        (state, _) = GameEngine.PlaceWonder(state, 0, Slot(1, 0), 0);
        var rule = Wonders.Info(WonderType.Cathedral).Cells[0];

        // Keep playing meadow tiles far from the blueprint and count how many dealt tiles would suit a plot.
        var suited = 0;
        for (var i = 0; i < 60; i++)
        {
            if (state.NextTile is TileSpec next && Wonders.Suits(rule, next))
                suited++;
            state.CurrentTile = TileSpec.Pure(Terrain.Grass);
            var spot = state.CandidateSlots().First(s => Wonders.PlotAt(state, s) is null);
            (state, _) = GameEngine.Place(state, spot, 0);
            state.StackCount = Math.Max(state.StackCount, 5);
        }

        Assert.True(suited >= 12, $"only {suited} of 60 tiles suited the cathedral plots");
    }

    [Fact]
    public void SecondMilestone_AwardsAWonderCard()
    {
        var grass = TileSpec.Pure(Terrain.Grass);
        var state = Board((Slot(0, 0), grass));
        state.Unlocked = Terrains.Starting.ToList();
        state.MilestonesReached = 1;
        state.Score = GameRules.MilestoneThreshold(1) - 5;

        var (next, events) = GameEngine.Place(state, Slot(1, 0), 0);

        var award = Assert.Single(events.OfType<WonderAwardedEvent>());
        Assert.Contains(award.Type, next.WonderCards);
        Assert.Contains(award.Type, Wonders.Available(next));
    }

    [Fact]
    public void EveryThirdQuest_AwardsAWonderCard()
    {
        var forest = TileSpec.Pure(Terrain.Forest);
        var state = Board((Slot(0, 0), forest));
        state.Stats = state.Stats with { QuestsCompleted = 2 };
        state.Quests.Add(new Quest(1, Terrain.Forest, QuestKind.AtLeast, 12, new SectorRef(Slot(0, 0), 0), 4, 60, 0));
        state.CurrentTile = forest;

        var (_, events) = GameEngine.Place(state, Slot(1, 0), 0);

        Assert.Equal("Quests", Assert.Single(events.OfType<WonderAwardedEvent>()).Reason);
    }

    [Fact]
    public void AvailableWonders_OnlyUseUnlockedTerrains()
    {
        var state = GameState.CreateInitial(3, WorldTheme.Temperate);
        var available = Wonders.Available(state);

        Assert.Contains(WonderType.Cathedral, available);
        Assert.Contains(WonderType.TallShip, available);
        Assert.DoesNotContain(WonderType.Castle, available);
        Assert.DoesNotContain(WonderType.Observatory, available);
    }

    [Fact]
    public void WonderCards_AndBlueprints_SurviveSaveLoad()
    {
        var path = Path.Combine(Path.GetTempPath(), $"lb-wonder-{Guid.NewGuid():N}.json");
        try
        {
            var state = Board((Slot(0, 0), TileSpec.Pure(Terrain.Village)));
            state.WonderCards.Add(WonderType.Cathedral);
            state.WonderCards.Add(WonderType.Harbour);
            (state, _) = GameEngine.PlaceWonder(state, 0, Slot(1, 0), 2);

            new SaveRepository().Save(path, state);
            Assert.True(new SaveRepository().TryLoad(path, out var loaded, out var error), error);

            Assert.Equal(state.Fingerprint(), loaded.Fingerprint());
            Assert.Equal([WonderType.Harbour], loaded.WonderCards);
            Assert.Single(loaded.Sites);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Bot_FindsALegalSite_PreferringSuitableTiles()
    {
        var village = TileSpec.Pure(Terrain.Village);
        var state = Board((Slot(0, 0), village), (Slot(1, 0), village));
        state.WonderCards.Add(WonderType.Cathedral);

        var best = GameEngine.FindBestSite(state, 0);

        Assert.NotNull(best);
        Assert.True(best!.IsValid);
        Assert.Equal(2, best.Ready);
    }
}
