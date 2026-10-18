using LandBuilder.Domain;
using Xunit;

namespace LandBuilder.Tests;

public sealed class ConnectionRewardTests
{
    private static int Slot(int q, int r) => HexGrid.Encode(new HexCoord(q, r));

    private static GameState Board(params (int Slot, TileSpec Tile)[] tiles)
    {
        var state = GameState.CreateInitial(7, WorldTheme.Temperate);
        state.Board.Clear();
        foreach (var (slot, tile) in tiles)
            state.Board[slot] = new PlacedTile(tile, 0, 0);
        return state;
    }

    [Fact]
    public void Ladder_ClimbsWithSize_AndMeadowsNeverLevel()
    {
        Assert.Equal(0, RegionTiers.Level(Terrain.Village, 7));
        Assert.Equal(1, RegionTiers.Level(Terrain.Village, 8));
        Assert.Equal("Town", RegionTiers.NameFor(Terrain.Village, 16));
        Assert.Equal("City", RegionTiers.NameFor(Terrain.Village, 30));
        Assert.Equal(RegionTiers.MaxLevel, RegionTiers.Level(Terrain.Village, 500));
        Assert.Null(RegionTiers.Next(Terrain.Village, 500));
        Assert.Equal(0, RegionTiers.Level(Terrain.Grass, 500));
        Assert.Equal("Hamlet", RegionTiers.NameFor(Terrain.Village, 3));
    }

    [Fact]
    public void GrowingAVillage_IntoATown_PaysPointsAndTiles()
    {
        var village = TileSpec.Pure(Terrain.Village);
        var state = Board((Slot(0, 0), village), (Slot(1, 0), village));
        state.CurrentTile = village;
        var score = state.Score;
        var stack = state.StackCount;

        var (next, events) = GameEngine.Place(state, Slot(0, 1), 0);

        // 12 sectors were a Village already; 18 makes a Town.
        var reached = Assert.Single(events.OfType<RegionTierReachedEvent>());
        Assert.Equal("Town", reached.Tier.Name);
        Assert.Equal(3, reached.Slots.Count);
        Assert.True(next.Score >= score + reached.Tier.Points);
        Assert.True(next.StackCount >= stack - 1 + reached.Tier.Tiles);
        Assert.Equal(2, next.Stats.BestVillage);
    }

    [Fact]
    public void MergingTwoRegions_PaysOnlyForTheNewRung()
    {
        var village = TileSpec.Pure(Terrain.Village);
        // Two separate 12-sector Villages, joined by one more village tile into a 30-sector City.
        var state = Board((Slot(0, 0), village), (Slot(1, 0), village), (Slot(3, 0), village), (Slot(4, 0), village));
        state.CurrentTile = village;

        var (_, events) = GameEngine.Place(state, Slot(2, 0), 0);

        var rungs = events.OfType<RegionTierReachedEvent>().Select(e => e.Tier.Name).ToArray();
        Assert.Equal(["Town", "City"], rungs);
    }

    [Fact]
    public void ClosingARegion_PaysByItsSize()
    {
        // A lake ring around a centre slot: placing a pure lake tile there encloses the whole lake.
        var lakeTile = TileSpec.Pure(Terrain.Water);
        var centre = Slot(0, 0);
        var state = Board();
        for (var e = 0; e < 6; e++)
        {
            var slot = HexGrid.NeighborSlot(centre, e);
            // Each ring tile faces the centre with water; its outward edges are grass.
            var sectors = Enumerable.Repeat(Terrain.Grass, 6).ToArray();
            sectors[HexGrid.OppositeEdge(e)] = Terrain.Water;
            state.Board[slot] = new PlacedTile(TileSpec.FromArray(sectors), 0, 0);
        }

        state.CurrentTile = lakeTile;
        var (_, events) = GameEngine.Place(state, centre, 0);

        var closed = Assert.Single(events.OfType<RegionClosedEvent>());
        Assert.Equal(Terrain.Water, closed.Terrain);
        Assert.Equal(12, closed.Size);
        Assert.Equal(12 * GameRules.PointsPerClosedSector, closed.Points);
        Assert.Equal(0, closed.Tiles);
    }
}
