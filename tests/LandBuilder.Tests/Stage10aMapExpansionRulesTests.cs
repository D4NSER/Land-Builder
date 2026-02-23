using System.Linq;
using LandBuilder.Domain;
using LandBuilder.Infrastructure.Content;
using Xunit;

namespace LandBuilder.Tests;

public class Stage10aMapExpansionRulesTests
{
    [Fact]
    public void ExpansionRules_AdjacencyLockedUnlockableInsufficientFundsAndDepthTierCosts()
    {
        var objectives = new ObjectiveDefinitionLoader().Load(TestPaths.ObjectivesJson);
        var state = GameState.CreateInitial(objectives);

        var tile1Preview = DeterministicSimulator.ValidateExpansion(state, 1);
        var tile3Preview = DeterministicSimulator.ValidateExpansion(state, 3);
        Assert.True(tile1Preview.IsValid);
        Assert.True(tile3Preview.IsValid);
        Assert.Equal(1, state.World.Tiles[1].RegionDepth);
        Assert.Equal(1, state.World.Tiles[3].RegionDepth);

        var lockedTile = DeterministicSimulator.ValidateExpansion(state, 2);
        Assert.False(lockedTile.IsValid);
        Assert.Equal(ValidationReasonCode.TileNotUnlockable, lockedTile.ReasonCode);

        var unlock1 = DeterministicSimulator.Apply(state, new ExpandTileCommand(1)).State;
        Assert.Equal(TileOwnership.Unlockable, unlock1.World.Tiles[2].Ownership);
        Assert.Equal(TileOwnership.Unlockable, unlock1.World.Tiles[4].Ownership);

        var fundsBlockedSeed = unlock1 with { Economy = unlock1.Economy with { Coins = 0 } };
        var insufficient = DeterministicSimulator.ValidateExpansion(fundsBlockedSeed, 4);
        Assert.False(insufficient.IsValid);
        Assert.Equal(ValidationReasonCode.InsufficientCoins, insufficient.ReasonCode);

        var depth2Preview = DeterministicSimulator.ValidateExpansion(unlock1, 4);
        Assert.True(depth2Preview.Cost > tile1Preview.Cost);
        Assert.Equal(2, unlock1.World.Tiles[4].RegionDepth);
    }
}
