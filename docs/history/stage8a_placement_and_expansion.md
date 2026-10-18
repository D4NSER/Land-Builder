# Stage 8A — Placement UX Clarity + Expansion Cost Scaling

## Scope lock
In-scope only:
- Build mode enter/exit with explicit cancel/back and no side-effects.
- Deterministic pre-click validity for placement and tile highlighting/readout.
- Clear tile state display (`Locked`, `Unlockable`, `Unlocked`, `Buildable`).
- Deterministic rejection reason codes/messages sourced from domain validation.
- Deterministic expansion cost preview that always matches command execution cost.

Out-of-scope:
- New building types.
- New progression content/objective chains.
- New economy systems or randomization.
- Stage 8B/8C work.

## File map
- `src/LandBuilder.Core/Domain/DeterministicSimulator.cs`
- `src/LandBuilder.Core/Domain/Events.cs`
- `src/LandBuilder.Core/Application/UiProjection.cs`
- `src/Presentation/MainController.cs`
- `scenes/main.tscn`
- `tests/LandBuilder.Tests/Stage8aPlacementValidationTests.cs`
- `tests/LandBuilder.Tests/Stage8aExpansionCostTests.cs`
- `README.md`

## Implementation summary
1. Added domain validation reason codes and deterministic validation APIs for:
   - placement (`ValidatePlacement`)
   - expansion (`ValidateExpansion`)
2. Unified rejection event payload with reason code + reason message.
3. Added deterministic expansion scaling function used by both preview and command apply paths.
4. Extended UI projection with per-tile state snapshots and validation/readiness flags.
5. Added build mode UX in presentation:
   - enter camp/quarry mode
   - cancel build mode (Esc/button)
   - tile action buttons show valid/invalid status before click
   - expansion buttons show dynamic preview costs
6. Added Stage 8A tests for placement matrix, expansion parity/scaling, determinism replay, and save/load persistence of expanded state + preview costs.

## Verification commands
- `dotnet build .\LandBuilder.sln`
- `dotnet test .\LandBuilder.sln --list-tests`
- `dotnet test .\LandBuilder.sln`

## Godot manual verification
1. Open project in Godot 4.x.
2. Run `scenes/main.tscn`.
3. Enter Camp mode (`C` or button), confirm tile action buttons mark `[VALID]`/`[INVALID]` and show deterministic reason code.
4. Press `Esc` (or Cancel button) and confirm build mode exits without placing anything.
5. Observe tile state panel (`Locked/Unlockable/Unlocked/Buildable`) updates after expansion/build actions.
6. Confirm expansion buttons show previewed costs; click expand and verify coin spend matches preview.

## Done criteria
- Build mode cancel/enter works with no side-effects.
- Pre-click tile validity and deterministic reason codes are visible.
- Expansion preview cost equals applied command cost.
- Required Stage 8A tests are discovered and pass.
