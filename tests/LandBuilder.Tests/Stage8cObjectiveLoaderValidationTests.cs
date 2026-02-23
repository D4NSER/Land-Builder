using System;
using System.IO;
using LandBuilder.Infrastructure.Content;
using Xunit;

namespace LandBuilder.Tests;

public class Stage8cObjectiveLoaderValidationTests
{
    [Fact]
    public void LoaderRejectsDuplicateIds_OutOfOrder_InvalidPredicate_MissingFields()
    {
        var loader = new ObjectiveDefinitionLoader();

        var duplicatePath = WriteTemp("""
            {"objectives":[
              {"objectiveId":"OBJ_01_A","type":"UnlockTileCount","targetValue":1,"rewardCoins":0},
              {"objectiveId":"OBJ_01_A","type":"UnlockTileCount","targetValue":1,"rewardCoins":0},
              {"objectiveId":"OBJ_03_C","type":"UnlockTileCount","targetValue":1,"rewardCoins":0},
              {"objectiveId":"OBJ_04_D","type":"UnlockTileCount","targetValue":1,"rewardCoins":0},
              {"objectiveId":"OBJ_05_E","type":"UnlockTileCount","targetValue":1,"rewardCoins":0},
              {"objectiveId":"OBJ_06_F","type":"UnlockTileCount","targetValue":1,"rewardCoins":0},
              {"objectiveId":"OBJ_07_G","type":"UnlockTileCount","targetValue":1,"rewardCoins":0},
              {"objectiveId":"OBJ_08_H","type":"UnlockTileCount","targetValue":1,"rewardCoins":0},
              {"objectiveId":"OBJ_09_I","type":"UnlockTileCount","targetValue":1,"rewardCoins":0},
              {"objectiveId":"OBJ_10_J","type":"UnlockTileCount","targetValue":1,"rewardCoins":0},
              {"objectiveId":"OBJ_11_K","type":"UnlockTileCount","targetValue":1,"rewardCoins":0},
              {"objectiveId":"OBJ_12_L","type":"UnlockTileCount","targetValue":1,"rewardCoins":0},
              {"objectiveId":"OBJ_13_M","type":"UnlockTileCount","targetValue":1,"rewardCoins":0},
              {"objectiveId":"OBJ_14_N","type":"UnlockTileCount","targetValue":1,"rewardCoins":0}
            ]}
            """);
        Assert.Throws<InvalidOperationException>(() => loader.Load(duplicatePath));

        var orderPath = WriteTemp("""
            {"objectives":[
              {"objectiveId":"OBJ_02_A","type":"UnlockTileCount","targetValue":1,"rewardCoins":0},
              {"objectiveId":"OBJ_01_B","type":"UnlockTileCount","targetValue":1,"rewardCoins":0},
              {"objectiveId":"OBJ_03_C","type":"UnlockTileCount","targetValue":1,"rewardCoins":0},
              {"objectiveId":"OBJ_04_D","type":"UnlockTileCount","targetValue":1,"rewardCoins":0},
              {"objectiveId":"OBJ_05_E","type":"UnlockTileCount","targetValue":1,"rewardCoins":0},
              {"objectiveId":"OBJ_06_F","type":"UnlockTileCount","targetValue":1,"rewardCoins":0},
              {"objectiveId":"OBJ_07_G","type":"UnlockTileCount","targetValue":1,"rewardCoins":0},
              {"objectiveId":"OBJ_08_H","type":"UnlockTileCount","targetValue":1,"rewardCoins":0},
              {"objectiveId":"OBJ_09_I","type":"UnlockTileCount","targetValue":1,"rewardCoins":0},
              {"objectiveId":"OBJ_10_J","type":"UnlockTileCount","targetValue":1,"rewardCoins":0},
              {"objectiveId":"OBJ_11_K","type":"UnlockTileCount","targetValue":1,"rewardCoins":0},
              {"objectiveId":"OBJ_12_L","type":"UnlockTileCount","targetValue":1,"rewardCoins":0},
              {"objectiveId":"OBJ_13_M","type":"UnlockTileCount","targetValue":1,"rewardCoins":0},
              {"objectiveId":"OBJ_14_N","type":"UnlockTileCount","targetValue":1,"rewardCoins":0}
            ]}
            """);
        Assert.Throws<InvalidOperationException>(() => loader.Load(orderPath));

        var missingFieldPath = WriteTemp("""
            {"objectives":[
              {"objectiveId":"OBJ_01_A","type":"PlaceBuildingTypeCount","targetValue":1,"rewardCoins":0},
              {"objectiveId":"OBJ_02_B","type":"UnlockTileCount","targetValue":1,"rewardCoins":0},
              {"objectiveId":"OBJ_03_C","type":"UnlockTileCount","targetValue":1,"rewardCoins":0},
              {"objectiveId":"OBJ_04_D","type":"UnlockTileCount","targetValue":1,"rewardCoins":0},
              {"objectiveId":"OBJ_05_E","type":"UnlockTileCount","targetValue":1,"rewardCoins":0},
              {"objectiveId":"OBJ_06_F","type":"UnlockTileCount","targetValue":1,"rewardCoins":0},
              {"objectiveId":"OBJ_07_G","type":"UnlockTileCount","targetValue":1,"rewardCoins":0},
              {"objectiveId":"OBJ_08_H","type":"UnlockTileCount","targetValue":1,"rewardCoins":0},
              {"objectiveId":"OBJ_09_I","type":"UnlockTileCount","targetValue":1,"rewardCoins":0},
              {"objectiveId":"OBJ_10_J","type":"UnlockTileCount","targetValue":1,"rewardCoins":0},
              {"objectiveId":"OBJ_11_K","type":"UnlockTileCount","targetValue":1,"rewardCoins":0},
              {"objectiveId":"OBJ_12_L","type":"UnlockTileCount","targetValue":1,"rewardCoins":0},
              {"objectiveId":"OBJ_13_M","type":"UnlockTileCount","targetValue":1,"rewardCoins":0},
              {"objectiveId":"OBJ_14_N","type":"UnlockTileCount","targetValue":1,"rewardCoins":0}
            ]}
            """);
        Assert.Throws<InvalidOperationException>(() => loader.Load(missingFieldPath));

        var invalidPredicatePath = WriteTemp("""
            {"objectives":[
              {"objectiveId":"OBJ_01_A","type":"UnlockTileCount","targetValue":1,"rewardCoins":0},
              {"objectiveId":"OBJ_02_B","type":"UnlockTileCount","targetValue":1,"rewardCoins":0},
              {"objectiveId":"OBJ_03_C","type":"UnlockTileCount","targetValue":1,"rewardCoins":0},
              {"objectiveId":"OBJ_04_D","type":"UnlockTileCount","targetValue":1,"rewardCoins":0},
              {"objectiveId":"OBJ_05_E","type":"UnlockTileCount","targetValue":1,"rewardCoins":0},
              {"objectiveId":"OBJ_06_F","type":"UnlockTileCount","targetValue":1,"rewardCoins":0},
              {"objectiveId":"OBJ_07_G","type":"UnlockTileCount","targetValue":1,"rewardCoins":0},
              {"objectiveId":"OBJ_08_H","type":"UnlockTileCount","targetValue":1,"rewardCoins":0},
              {"objectiveId":"OBJ_09_I","type":"UnlockTileCount","targetValue":1,"rewardCoins":0},
              {"objectiveId":"OBJ_10_J","type":"UnlockTileCount","targetValue":1,"rewardCoins":0},
              {"objectiveId":"OBJ_11_K","type":"UnlockTileCount","targetValue":1,"rewardCoins":0},
              {"objectiveId":"OBJ_12_L","type":"UnlockTileCount","targetValue":1,"rewardCoins":0},
              {"objectiveId":"OBJ_13_M","type":"UnlockTileCount","targetValue":1,"rewardCoins":0},
              {"objectiveId":"OBJ_14_N","type":"PlaceBuildingTypeCount","targetValue":2,"buildingTypeId":"MIXED_ALL","rewardCoins":0}
            ]}
            """);
        Assert.Throws<InvalidOperationException>(() => loader.Load(invalidPredicatePath));
    }

    private static string WriteTemp(string json)
    {
        var path = Path.Combine(Path.GetTempPath(), $"objective_loader_{Guid.NewGuid():N}.json");
        File.WriteAllText(path, json);
        return path;
    }
}
