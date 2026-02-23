# Stage 10C — Economy Pacing and Continuity (Deterministic Tuning Only)

## Tuned parameters (constants only)
`src/LandBuilder.Core/Domain/DeterministicSimulator.cs`

- Expansion cost curve (same deterministic formula, tuned coefficients)
  - `cost = baseUnlockCost + (RegionDepth * depthStep) + (max(0, nextUnlockIndex - 1) * indexStep)`
  - `depthStep`: `1 -> 2`
  - `indexStep`: kept `1`
- Building placement costs
  - Camp: `10 -> 11`
  - Quarry: `18 -> 20`
  - Sawmill: `22 -> 24`
  - Forester: `20 -> 22`
  - ClayWorks: `26 -> 28`
- Upgrade costs
  - Camp: `[8, 14] -> [9, 15]`
  - Quarry: `[12, 20] -> [13, 22]`
  - Sawmill: `[14, 23] -> [15, 25]`
  - Forester: `[12, 18] -> [13, 19]`
  - ClayWorks: `[16, 26] -> [17, 27]`
- Production scaling constants are unchanged from Stage 10B formulas:
  - Camp `1 * level`
  - Quarry `2 * level`
  - Sawmill `3 * level`
  - Forester `2 * level`
  - ClayWorks `4 * level`

## Pacing invariants
- Expansion cost is deterministic and monotonic with deeper `RegionDepth`.
- Expansion cost never decreases when unlock index increases for same tile/depth assumptions.
- Upgrade costs are deterministic and non-decreasing per building level.
- Total production per tick remains exactly equal to the sum of per-building contributions.

## Objective continuity (Stage 8C)
- Objective order and evaluation logic are unchanged.
- No objective IDs, objective count, or predicate types were changed.
- Continuity is validated with deterministic command streams that still complete the full chain without deadlock.

## Save safety and recovery
- Save schema is unchanged in Stage 10C.
- Atomic write + backup fallback paths are exercised by continuity regression tests.

## File map
- `src/LandBuilder.Core/Domain/DeterministicSimulator.cs`
- `tests/LandBuilder.Tests/Stage10cEconomyPacingTests.cs`
- `tests/LandBuilder.Tests/Stage10cProgressionContinuityTests.cs`
- `tests/LandBuilder.Tests/Stage10cSaveLoadContinuityTests.cs`

## Verification
- `dotnet build .\LandBuilder.sln`
- `dotnet test .\LandBuilder.sln --list-tests`
- `dotnet test .\LandBuilder.sln`
