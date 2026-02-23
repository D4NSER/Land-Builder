using System.Linq;
using System.Collections.Generic;
using LandBuilder.Domain;
using LandBuilder.Infrastructure.Content;
using Xunit;

namespace LandBuilder.Tests;

public class Stage10bProductionFormulaTests
{
    [Fact]
    public void PerBuildingPerLevel_ContributionFormulas_AreDeterministicAndSummedExactly()
    {
        var objectives = new ObjectiveDefinitionLoader().Load(TestPaths.ObjectivesJson);
        var state = GameState.CreateInitial(objectives) with
        {
            Buildings = new Dictionary<int, BuildingState>
            {
                [1] = new(1, "Camp", 0, 3),      // 3
                [2] = new(2, "Quarry", 2, 2),    // 4
                [3] = new(3, "Sawmill", 4, 2),   // 6
                [4] = new(4, "Forester", 3, 3),  // 6
                [5] = new(5, "ClayWorks", 5, 2)  // 8
            }
        };

        var contributions = DeterministicSimulator.GetBuildingContributions(state);

        Assert.Equal(3, contributions.Single(c => c.BuildingTypeId == "Camp").Contribution);
        Assert.Equal(4, contributions.Single(c => c.BuildingTypeId == "Quarry").Contribution);
        Assert.Equal(6, contributions.Single(c => c.BuildingTypeId == "Sawmill").Contribution);
        Assert.Equal(6, contributions.Single(c => c.BuildingTypeId == "Forester").Contribution);
        Assert.Equal(8, contributions.Single(c => c.BuildingTypeId == "ClayWorks").Contribution);

        var expected = 3 + 4 + 6 + 6 + 8;
        Assert.Equal(expected, DeterministicSimulator.GetProductionPerTick(state));
        Assert.Equal(expected, contributions.Sum(c => c.Contribution));
    }
}
