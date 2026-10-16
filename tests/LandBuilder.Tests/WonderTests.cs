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
        return state;
    }

    [Fact]
    public void PlacingAWonder_UsesTheCard_NotTheStack()
    {
        var state = Board((Slot(0, 0), TileSpec.Pure(Terrain.Village)));
        state.WonderCards.Add(WonderType.Cathedral);
        var hand = state.CurrentTile;
        var stack = state.StackCount;

        var (next, events) = GameEngine.PlaceWonder(state, 0, Slot(1, 0), 0);

        Assert.Single(events.OfType<WonderPlacedEvent>());
        Assert.Empty(next.WonderCards);
        Assert.Equal(hand, next.CurrentTile);
        Assert.Equal(stack, next.StackCount);
        Assert.Equal(WonderType.Cathedral, next.Board[Slot(1, 0)].Wonder);
        Assert.False(next.Board[Slot(1, 0)].Built);
        Assert.Equal(0, next.Stats.TilesPlaced);
    }

    [Fact]
    public void Wonder_CompletesWhenNeighboursMeetTheRecipe_AndPaysOnce()
    {
        // Cathedral needs five village edges facing it. Four village neighbours are already there.
        var village = TileSpec.Pure(Terrain.Village);
        var centre = Slot(0, 0);
        var tiles = Enumerable.Range(0, 4).Select(e => (HexGrid.NeighborSlot(centre, e), village)).ToArray();
        var state = Board(tiles);
        state.WonderCards.Add(WonderType.Cathedral);

        (state, var placed) = GameEngine.PlaceWonder(state, 0, centre, 0);
        Assert.Empty(placed.OfType<WonderCompletedEvent>());
        Assert.False(Wonders.Progress(state, centre).Complete);

        state.CurrentTile = village;
        var score = state.Score;
        (state, var events) = GameEngine.Place(state, HexGrid.NeighborSlot(centre, 4), 0);

        var done = Assert.Single(events.OfType<WonderCompletedEvent>());
        Assert.Equal(WonderType.Cathedral, done.Type);
        Assert.True(state.Board[centre].Built);
        Assert.Equal(1, state.Stats.WondersBuilt);
        Assert.True(state.Score >= score + done.Points);

        state.CurrentTile = village;
        (_, var later) = GameEngine.Place(state, HexGrid.NeighborSlot(centre, 5), 0);
        Assert.Empty(later.OfType<WonderCompletedEvent>());
    }

    [Fact]
    public void SecondMilestone_AwardsAWonderCard()
    {
        var grass = TileSpec.Pure(Terrain.Grass);
        var state = Board((Slot(0, 0), grass));
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
        Assert.DoesNotContain(WonderType.Castle, available);
        Assert.DoesNotContain(WonderType.Observatory, available);
    }

    [Fact]
    public void WonderCards_AndPlacedWonders_SurviveSaveLoad()
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
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Bot_FindsALegalWonderSpot()
    {
        var state = Board((Slot(0, 0), TileSpec.Pure(Terrain.Village)), (Slot(1, 0), TileSpec.Pure(Terrain.Village)));
        state.WonderCards.Add(WonderType.Cathedral);

        var best = GameEngine.FindBestWonderPlacement(state, 0);

        Assert.NotNull(best);
        Assert.True(best!.IsValid);
        Assert.Equal(2, best.MatchedEdges.Count);
    }
}
