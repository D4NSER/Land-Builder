using System;
using System.Collections.Generic;
using System.Linq;
using LandBuilder.Domain;
using LandBuilder.Infrastructure.Content;
using Xunit;

namespace LandBuilder.Tests;

public class Stage8cObjectiveBoundaryTests
{
    [Fact]
    public void Chapter2ObjectiveBoundaries_JustBelowVsAtThreshold()
    {
        var objectives = new ObjectiveDefinitionLoader().Load(TestPaths.ObjectivesJson);

        AssertBoundary(CreateStage7BoundarySeed(objectives),
            s => s,
            s => DeterministicSimulator.Apply(s, new ExpandTileCommand(2)).State,
            6);

        AssertBoundary(CreateBaseState(objectives) with { Progression = CreateProgression(7) },
            s => s,
            s => DeterministicSimulator.Apply(s, new PlaceBuildingCommand("Camp", 0)).State,
            7);

        AssertBoundary(CreateBaseState(objectives) with { Progression = CreateProgression(8) },
            s => s,
            s => DeterministicSimulator.Apply(PrepareUnlockForQuarry(s), new PlaceBuildingCommand("Quarry", 2)).State,
            8);

        AssertBoundary(CreateBaseState(objectives) with { Progression = CreateProgression(9, includeQuarryUnlock: true) },
            s => DeterministicSimulator.Apply(s, new PlaceBuildingCommand("Quarry", 2)).State,
            s =>
            {
                var q = DeterministicSimulator.Apply(s, new PlaceBuildingCommand("Quarry", 2)).State;
                return DeterministicSimulator.Apply(q, new PlaceBuildingCommand("Sawmill", 1)).State;
            },
            9);

        AssertBoundary(CreateBaseState(objectives) with { Progression = CreateProgression(10, includeQuarryUnlock: true) },
            s =>
            {
                var q = DeterministicSimulator.Apply(s, new PlaceBuildingCommand("Quarry", 2)).State;
                return DeterministicSimulator.Apply(q, new PlaceBuildingCommand("Sawmill", 1)).State;
            },
            s =>
            {
                var q = DeterministicSimulator.Apply(s, new PlaceBuildingCommand("Quarry", 2)).State;
                var sm = DeterministicSimulator.Apply(q, new PlaceBuildingCommand("Sawmill", 1)).State;
                return DeterministicSimulator.Apply(sm, new UpgradeBuildingCommand(2)).State;
            },
            10);

        AssertBoundary(CreateBaseState(objectives) with
            {
                Progression = CreateProgression(11),
                Economy = new EconomyState(300, 59)
            },
            s => s,
            s => s with { Economy = new EconomyState(s.Economy.Coins, 60) },
            11);

        AssertBoundary(PrepareProductionState(objectives, sawmillLevel2: false) with { Progression = CreateProgression(12) },
            s => s,
            s => PrepareProductionState(objectives, sawmillLevel2: true) with { Progression = CreateProgression(12) },
            12);

        AssertBoundary(CreateBaseState(objectives) with { Progression = CreateProgression(13, includeQuarryUnlock: true) },
            s =>
            {
                var q = DeterministicSimulator.Apply(s, new PlaceBuildingCommand("Quarry", 2)).State;
                return q;
            },
            s =>
            {
                var camp = DeterministicSimulator.Apply(s, new PlaceBuildingCommand("Camp", 0)).State;
                var q = DeterministicSimulator.Apply(camp, new PlaceBuildingCommand("Quarry", 2)).State;
                return DeterministicSimulator.Apply(q, new PlaceBuildingCommand("Sawmill", 1)).State;
            },
            13);
    }

    private static void AssertBoundary(GameState seed, Func<GameState, GameState> belowBuilder, Func<GameState, GameState> atBuilder, int objectiveIndex)
    {
        var belowState = belowBuilder(seed);
        belowState = DeterministicSimulator.Apply(belowState, new TickCommand(1)).State;
        Assert.Equal(objectiveIndex, belowState.Progression.CurrentObjectiveIndex);

        var atState = atBuilder(seed);
        atState = DeterministicSimulator.Apply(atState, new TickCommand(1)).State;
        Assert.True(atState.Progression.CurrentObjectiveIndex > objectiveIndex);
    }

    private static GameState CreateStage7BoundarySeed(IReadOnlyList<ObjectiveDefinition> objectives)
    {
        var state = GameState.CreateInitial(objectives) with { Economy = new EconomyState(500, 0) };
        state = DeterministicSimulator.Apply(state, new ExpandTileCommand(1)).State;
        return state with { Progression = CreateProgression(6) };
    }

    private static GameState CreateBaseState(IReadOnlyList<ObjectiveDefinition> objectives)
    {
        var state = GameState.CreateInitial(objectives) with { Economy = new EconomyState(500, 0) };
        state = DeterministicSimulator.Apply(state, new ExpandTileCommand(1)).State;
        state = DeterministicSimulator.Apply(state, new ExpandTileCommand(2)).State;
        return state;
    }

    private static GameState PrepareUnlockForQuarry(GameState state)
    {
        return state with
        {
            Progression = state.Progression with
            {
                UnlockFlags = state.Progression.UnlockFlags.Append("UNLOCK_QUARRY").ToHashSet()
            }
        };
    }

    private static GameState PrepareProductionState(IReadOnlyList<ObjectiveDefinition> objectives, bool sawmillLevel2)
    {
        var state = PrepareUnlockForQuarry(CreateBaseState(objectives));
        state = DeterministicSimulator.Apply(state, new PlaceBuildingCommand("Camp", 0)).State;
        state = DeterministicSimulator.Apply(state, new PlaceBuildingCommand("Quarry", 2)).State;
        state = DeterministicSimulator.Apply(state, new PlaceBuildingCommand("Sawmill", 1)).State;

        if (sawmillLevel2)
        {
            state = DeterministicSimulator.Apply(state, new UpgradeBuildingCommand(3)).State;
        }

        return state;
    }

    private static ProgressionState CreateProgression(int objectiveIndex, bool includeQuarryUnlock = false)
    {
        var unlocks = new HashSet<string>();
        if (includeQuarryUnlock)
            unlocks.Add("UNLOCK_QUARRY");

        return new ProgressionState(objectiveIndex, new List<string>(), unlocks, string.Empty, string.Empty);
    }
}
