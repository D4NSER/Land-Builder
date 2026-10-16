using LandBuilder.Domain;
using Xunit;

namespace LandBuilder.Tests;

public sealed class RiverAndThemeTests
{
    private static int Slot(int q, int r) => HexGrid.Encode(new HexCoord(q, r));

    private static TileSpec RiverThrough(int a, int b, Terrain land = Terrain.Grass)
    {
        var sectors = Enumerable.Repeat(land, 6).ToArray();
        sectors[a] = Terrain.River;
        sectors[b] = Terrain.River;
        return TileSpec.FromArray(sectors);
    }

    private static GameState Board(TileSpec hand, params (int Slot, TileSpec Tile, int Rotation)[] tiles)
    {
        var state = GameState.CreateInitial(42, WorldTheme.Temperate);
        state.Board.Clear();
        foreach (var (slot, tile, rotation) in tiles)
            state.Board[slot] = new PlacedTile(tile, rotation, 0);
        state.CurrentTile = hand;
        return state;
    }

    [Fact]
    public void RiverEdges_MustMeetRiverOrLake()
    {
        // Origin has a river leaving through edge 0, i.e. towards (1,0).
        var state = Board(RiverThrough(0, 3), (Slot(0, 0), RiverThrough(0, 3), 0));

        // Rotation 0: the new tile's edge 3 (river) faces the origin's river edge 0.
        var connected = GameEngine.Evaluate(state, Slot(1, 0), 0);
        Assert.True(connected.IsValid);
        Assert.Equal([3], connected.MatchedEdges);

        // Rotation 1 puts grass against the river: illegal.
        Assert.Equal(PlacementRejectionReason.RiverBlocked, GameEngine.Evaluate(state, Slot(1, 0), 1).RejectionReason);

        // A plain grass tile can't sit against the river mouth either, but is fine elsewhere.
        state.CurrentTile = TileSpec.Pure(Terrain.Grass);
        Assert.Equal(PlacementRejectionReason.RiverBlocked, GameEngine.Evaluate(state, Slot(1, 0), 0).RejectionReason);
        Assert.True(GameEngine.Evaluate(state, Slot(0, -1), 0).IsValid);
    }

    [Fact]
    public void Rivers_FlowIntoLakes()
    {
        var state = Board(TileSpec.Pure(Terrain.Water), (Slot(0, 0), RiverThrough(0, 3), 0));

        var eval = GameEngine.Evaluate(state, Slot(1, 0), 0);

        Assert.True(eval.IsValid);
        Assert.Equal([3], eval.MatchedEdges);
    }

    [Fact]
    public void RiverWedgesInOneTile_FormOneChannel()
    {
        var board = new Dictionary<int, PlacedTile>
        {
            [Slot(0, 0)] = new(RiverThrough(0, 3), 0, 0),
            [Slot(1, 0)] = new(RiverThrough(3, 0), 0, 0)
        };

        var region = RegionMap.Build(board).RegionAt(new SectorRef(Slot(0, 0), 3))!;

        Assert.Equal(Terrain.River, region.Terrain);
        Assert.Equal(4, region.Size);
    }

    [Fact]
    public void UnplaceableTiles_AreSetAside_InsteadOfStrandingThePlayer()
    {
        var grass = TileSpec.Pure(Terrain.Grass);
        var state = Board(grass, (Slot(0, 0), grass, 0));
        state.NextTile = TileSpec.Pure(Terrain.River);

        var (next, events) = GameEngine.Place(state, Slot(1, 0), 0);

        var discarded = Assert.Single(events.OfType<TileDiscardedEvent>());
        Assert.Equal(TileSpec.Pure(Terrain.River), discarded.Tile);
        Assert.NotEqual(TileSpec.Pure(Terrain.River), next.CurrentTile);
        Assert.True(GameEngine.HasAnyValidPlacement(next));
    }

    [Fact]
    public void Generator_LaysRiversOnlyOnceUnlocked()
    {
        var withoutRiver = new[] { Terrain.Grass, Terrain.Forest, Terrain.Water };
        var withRiver = withoutRiver.Append(Terrain.River).ToArray();
        var rng = new Rng(5);
        var riverTiles = 0;
        for (var i = 0; i < 2000; i++)
        {
            Assert.DoesNotContain(Terrain.River, TileGenerator.Generate(ref rng, withoutRiver, i).ToArray());
            var tile = TileGenerator.Generate(ref rng, withRiver, i);
            var rivers = tile.ToArray().Count(t => t == Terrain.River);
            Assert.InRange(rivers, 0, 3);
            if (rivers > 0)
                riverTiles++;
        }

        Assert.InRange(riverTiles, 250, 550);
    }

    [Fact]
    public void Themes_AreDeterministicPerSeed_AndAllAppear()
    {
        Assert.Equal(Themes.FromSeed(99), Themes.FromSeed(99));
        var seen = Enumerable.Range(1, 200).Select(s => Themes.FromSeed((ulong)s)).Distinct().ToArray();
        Assert.Equal(Themes.All.Count, seen.Length);
        Assert.Equal(Themes.FromSeed(1234), GameState.CreateInitial(1234).Theme);
    }

    [Theory]
    [InlineData(WorldTheme.Nordic, 2, Terrain.Snow)]
    [InlineData(WorldTheme.Tropical, 0, Terrain.Sand)]
    [InlineData(WorldTheme.Autumn, 0, Terrain.Orchard)]
    [InlineData(WorldTheme.Temperate, 1, Terrain.River)]
    public void Milestones_UnlockThemeTerrains(WorldTheme theme, int milestone, Terrain expected)
    {
        Assert.Equal(expected, GameRules.MilestoneUnlock(theme, milestone));
    }

    [Fact]
    public void ThemeWeights_RemoveSnowFromTropicalIslands()
    {
        var unlocked = new[] { Terrain.Grass, Terrain.Snow, Terrain.Water };
        var rng = new Rng(77);
        for (var i = 0; i < 500; i++)
        {
            var tile = TileGenerator.Generate(ref rng, unlocked, i, theme: WorldTheme.Tropical);
            Assert.DoesNotContain(Terrain.Snow, tile.ToArray());
        }
    }
}
