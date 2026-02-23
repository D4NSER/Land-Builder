using System.Linq;
using LandBuilder.Domain;
using LandBuilder.Infrastructure.Content;
using Xunit;

namespace LandBuilder.Tests;

public class Stage8aPlacementValidationTests
{
    [Fact]
    public void PlacementValidationMatrix_ReturnsDeterministicReasonCodes()
    {
        var objectives = new ObjectiveDefinitionLoader().Load(TestPaths.ObjectivesJson);
        var initial = GameState.CreateInitial(objectives);

        var lockedTile = DeterministicSimulator.ValidatePlacement(initial, "Camp", 2);
        var unlockableTile = DeterministicSimulator.ValidatePlacement(initial, "Camp", 1);

        Assert.Equal(ValidationReasonCode.TileNotBuildable, lockedTile.ReasonCode);
        Assert.Equal(TileStateKind.Locked, lockedTile.TileState);
        Assert.Equal(ValidationReasonCode.TileNotBuildable, unlockableTile.ReasonCode);
        Assert.Equal(TileStateKind.Unlockable, unlockableTile.TileState);

        var slotState = DeterministicSimulator.Apply(initial, new PlaceBuildingCommand("Camp", 0)).State;
        var noSlot = DeterministicSimulator.ValidatePlacement(slotState, "Camp", 0);
        Assert.Equal(ValidationReasonCode.NoBuildingSlotsAvailable, noSlot.ReasonCode);

        var unlockedState = DeterministicSimulator.Apply(initial, new ExpandTileCommand(1)).State;
        unlockedState = DeterministicSimulator.Apply(unlockedState, new ExpandTileCommand(2)).State;

        var quarryBlocked = DeterministicSimulator.ValidatePlacement(unlockedState, "Quarry", 2);
        Assert.Equal(ValidationReasonCode.BuildingTypeNotUnlocked, quarryBlocked.ReasonCode);

        var quarryUnlockedState = unlockedState with
        {
            Economy = unlockedState.Economy with { Coins = 999 },
            Progression = unlockedState.Progression with
            {
                UnlockFlags = unlockedState.Progression.UnlockFlags.Append("UNLOCK_QUARRY").ToHashSet()
            }
        };
        var quarryValid = DeterministicSimulator.ValidatePlacement(quarryUnlockedState, "Quarry", 2);
        Assert.True(quarryValid.IsValid);
        Assert.Equal(ValidationReasonCode.None, quarryValid.ReasonCode);
    }
}
