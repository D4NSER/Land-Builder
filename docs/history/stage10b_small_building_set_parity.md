# Stage 10B — Small Building Set Parity (Exactly +2 Buildings)

## Building definitions
Existing:
- Camp (Grass)
- Quarry (Rocky, requires `UNLOCK_QUARRY`)
- Sawmill (Grass, requires `Quarry >= 1`)

Added in Stage 10B:
- **Forester**
  - Terrain: `Forest`
  - Prerequisite: `Sawmill >= 1`
  - Base cost: `20`
  - Production formula: `2 * level`
  - Upgrade costs: `12`, `18`
- **ClayWorks**
  - Terrain: `Clay`
  - Prerequisites: `UNLOCK_QUARRY` and `Quarry >= 1`
  - Base cost: `26`
  - Production formula: `4 * level`
  - Upgrade costs: `16`, `26`

Total building types after Stage 10B: **5**.

## Deterministic gating + messages
Uses existing validation primitives/reason codes only:
- `BuildingTypeNotUnlocked`
- `TerrainMismatch`
- `MissingPrerequisiteBuildingCount` (message e.g. `Requires at least 1 Sawmill`)

## Production transparency
Total production per tick remains:

```text
total = sum(baseProductionPerTick * level for each building)
```

Per-building contributions are exposed through `UiProjection.BuildingContributions`.

## File map
- `src/LandBuilder.Core/Domain/DeterministicSimulator.cs`
- `tests/LandBuilder.Tests/Stage10bBuildingRulesTests.cs`
- `tests/LandBuilder.Tests/Stage10bProductionFormulaTests.cs`
- `tests/LandBuilder.Tests/Stage10bProductionDeterminismTests.cs`
- `tests/LandBuilder.Tests/Stage10bSaveLoadRoundTripTests.cs`

## Verification
- `dotnet build .\LandBuilder.sln`
- `dotnet test .\LandBuilder.sln --list-tests`
- `dotnet test .\LandBuilder.sln`
