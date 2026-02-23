# Stage 10A — Map Loop Expansion (Deterministic Multi-Pocket Topology)

## Topology
Deterministic 9-tile graph with fixed IDs and adjacency:
- `0`: `[1,3]` (initial unlocked)
- `1`: `[0,2,4]`
- `2`: `[1,5]`
- `3`: `[0,4,6]`
- `4`: `[1,3,5,7]`
- `5`: `[2,4,8]`
- `6`: `[3,7]`
- `7`: `[4,6,8]`
- `8`: `[5,7]`

Initial ownership:
- Unlocked: `0`
- Unlockable: `1,3`
- Locked: others

## Region depth/tier metadata
Each tile has deterministic `RegionDepth` metadata:
- Depth 0: `0`
- Depth 1: `1,3`
- Depth 2: `2,4,6`
- Depth 3: `5,7`
- Depth 4: `8`

## Unlock cost function
Expansion preview/apply uses the same deterministic formula:

```text
cost = baseUnlockCost + (RegionDepth * 1) + (max(0, nextUnlockIndex - 1) * 1)
```

Where `nextUnlockIndex` is the number of currently unlocked tiles before the command.

## File map
- `src/LandBuilder.Core/Domain/GameState.cs`
- `src/LandBuilder.Core/Domain/DeterministicSimulator.cs`
- `src/LandBuilder.Core/Infrastructure/SaveRepository.cs`
- `tests/LandBuilder.Tests/Stage10aMapExpansionRulesTests.cs`
- `tests/LandBuilder.Tests/Stage10aDeterminismReplayTests.cs`
- `tests/LandBuilder.Tests/Stage10aSaveLoadMapTests.cs`
- `tests/LandBuilder.Tests/Stage8aExpansionCostTests.cs`
- `tests/LandBuilder.Tests/Mvp0SaveRoundTripTests.cs`

## Verification
- `dotnet build .\LandBuilder.sln`
- `dotnet test .\LandBuilder.sln --list-tests`
- `dotnet test .\LandBuilder.sln`
