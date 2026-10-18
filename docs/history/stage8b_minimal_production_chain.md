# Stage 8B — Minimal Multi-Building Production Chain

## Building definitions (deterministic)
- Camp: base production `1 * level`
- Quarry: base production `2 * level` (requires `UNLOCK_QUARRY`, Rocky terrain)
- Sawmill (new): base production `3 * level` (requires at least `1` Quarry, Grass terrain)

Total production per tick is deterministic and equals the sum of per-building contributions.

## Gating rules
- Existing unlock-flag + terrain gates remain unchanged for Quarry.
- New prerequisite-count gate for Sawmill:
  - `RequiredBuildingTypeId = Quarry`
  - `RequiredBuildingCount = 1`
  - deterministic rejection code: `MissingPrerequisiteBuildingCount`
  - deterministic rejection message: `Requires at least 1 Quarry`

## File map
- `src/LandBuilder.Core/Domain/DeterministicSimulator.cs`
- `src/LandBuilder.Core/Application/UiProjection.cs`
- `src/Presentation/MainController.cs`
- `scenes/main.tscn`
- `tests/LandBuilder.Tests/Stage8bRulesTests.cs`
- `tests/LandBuilder.Tests/Stage8bDeterminismStressTests.cs`
- `tests/LandBuilder.Tests/Stage8bSaveLoadTests.cs`
- `README.md`

## Verification commands
- `dotnet build .\LandBuilder.sln`
- `dotnet test .\LandBuilder.sln --list-tests`
- `dotnet test .\LandBuilder.sln`
