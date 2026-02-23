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

        // Find a seed that draws Plains first and Village second for deterministic incompatibility at slot1 next to slot0.
        ulong seed = 1;
        while (true)
        {
            state = GameState.CreateInitial(seed);
            (state, _) = sim.Apply(state, new DrawTileCommand());
            if (state.CurrentTile == TileType.Plains)
            {
                (state, _) = sim.Apply(state, new PlaceTileCommand(0));
                (state, _) = sim.Apply(state, new DrawTileCommand());
                if (state.CurrentTile == TileType.Village)
                    break;
            }

            seed++;
        }

        var (_, events) = sim.Apply(state, new PlaceTileCommand(1));
        var rejection = Assert.IsType<PlacementRejectedEvent>(events.OfType<PlacementRejectedEvent>().First());
        Assert.Equal(PlacementRejectionReason.IncompatibleAdjacent, rejection.Reason);
    }
}
