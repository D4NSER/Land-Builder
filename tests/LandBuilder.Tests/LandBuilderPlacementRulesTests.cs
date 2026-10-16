using LandBuilder.Domain;
using Xunit;

namespace LandBuilder.Tests;

public class LandBuilderPlacementRulesTests
{
    [Fact]
    public void PlaceWithoutDraw_IsRejectedWithNoTileReason()
    {
        var sim = new DeterministicSimulator();
        var (state, events) = sim.Apply(GameState.CreateInitial(1), new PlaceTileCommand(0));

        var rejection = Assert.IsType<PlacementRejectedEvent>(events.OfType<PlacementRejectedEvent>().First());
        Assert.Equal(PlacementRejectionReason.NoTileInHand, rejection.Reason);
        Assert.Empty(state.Board);
    }

    [Fact]
    public void IncompatibleAdjacency_IsRejectedDeterministically()
    {
        var sim = new DeterministicSimulator();
        var state = GameState.CreateInitial(1);
        state.Board[HexGrid.OriginSlotIndex] = new PlacedTile(TileType.Plains, 0);
        state.CurrentTile = TileType.Plains;

        var farAwaySlot = HexGrid.Encode(new HexCoord(10, -7));
        var (_, events) = sim.Apply(state, new PlaceTileCommand(farAwaySlot));

        var rejection = Assert.IsType<PlacementRejectedEvent>(events.OfType<PlacementRejectedEvent>().First());
        Assert.Equal(PlacementRejectionReason.IncompatibleAdjacent, rejection.Reason);
    }
}
