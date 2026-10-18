# Stage 8C — Chapter 2 Objectives (Deterministic Extension)

## Objective chain (8 objectives)
1. `OBJ_07_CH2_UNLOCK_ALL_TILES` — `UnlockTileCount >= 2`
2. `OBJ_08_CH2_KEEP_ONE_CAMP` — `PlaceBuildingTypeCount(Camp) >= 1`
3. `OBJ_09_CH2_KEEP_ONE_QUARRY` — `PlaceBuildingTypeCount(Quarry) >= 1`
4. `OBJ_10_CH2_BUILD_SAWMILL` — `PlaceBuildingTypeCount(Sawmill) >= 1`
5. `OBJ_11_CH2_UPGRADE_SAWMILL_L2` — `UpgradeBuildingToLevelAtLeast(Sawmill, 2)`
6. `OBJ_12_CH2_LIFETIME_60` — `LifetimeCoinsEarned >= 60`
7. `OBJ_13_CH2_PRODUCTION_9` — `ProductionPerTickAtLeast >= 9`
8. `OBJ_14_CH2_MIXED_ONE_EACH` — `PlaceBuildingTypeCount(MIXED_ALL)` interpreted as at least one Camp, Quarry, and Sawmill.

All objectives are index-based and evaluated deterministically in order.

## File map
- `data/objectives/mvp2_objectives.json`
- `src/LandBuilder.Core/Domain/DeterministicSimulator.cs`
- `src/LandBuilder.Core/Infrastructure/Content/ObjectiveDefinitionLoader.cs`
- `tests/LandBuilder.Tests/Mvp2ProgressionRulesTests.cs`
- `tests/LandBuilder.Tests/Stage8cProgressionDeterminismTests.cs`
- `tests/LandBuilder.Tests/Stage8cObjectiveBoundaryTests.cs`
- `tests/LandBuilder.Tests/Stage8cSaveLoadProgressionTests.cs`
- `tests/LandBuilder.Tests/Stage8cObjectiveLoaderValidationTests.cs`

## Verification commands
- `dotnet build .\LandBuilder.sln`
- `dotnet test .\LandBuilder.sln --list-tests`
- `dotnet test .\LandBuilder.sln`
