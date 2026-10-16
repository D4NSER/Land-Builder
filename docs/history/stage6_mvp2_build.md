# Stage 6 – MVP-2 Build (Progression + Save/Load Reliability)

## 1) MVP-2 Scope (Locked)

### In scope
- Deterministic 6-step objective chain implemented exactly as Stage 4 defines.
- Deterministic objective evaluation order (single ordered chain, no unordered iteration).
- Reward types limited to coins + unlock flags.
- Data-driven objective definitions via minimal JSON schema + fail-fast validation.
- Save schema v3 update for progression state and unlock flags.
- v2 -> v3 migration path + fixture test.
- Minimal UI additions for active objective, objective progress, and last completion message.

### Out of scope
- MVP-3 stabilization/polish.
- Offline rewards, multi-region support, advanced scripting system.
- Any reward types beyond coins and unlock flags.

---

## 2) File Map

### Domain
- `src/Domain/ProgressionModels.cs` (new)
- `src/Domain/GameState.cs`
- `src/Domain/Events.cs`
- `src/Domain/DeterministicSimulator.cs`

### Infrastructure
- `src/Infrastructure/Content/ObjectiveDefinitionLoader.cs` (new)
- `data/objectives/mvp2_objectives.json` (new)
- `src/Infrastructure/SaveRepository.cs`

### Application
- `src/Application/UiProjection.cs`

### Presentation
- `src/Presentation/MainController.cs`
- `scenes/main.tscn`

### Tests
- `tests/Mvp0DeterminismTests.cs`
- `tests/Mvp0SaveRoundTripTests.cs`
- `tests/Mvp1DomainRulesTests.cs`
- `tests/Mvp2ProgressionRulesTests.cs` (new)
- `tests/Mvp2SaveMigrationTests.cs` (new)
- `tests/fixtures/saves/v2_mvp1_save.json` (new)

---

## 3) Implementation Steps (Dependency Order)

1. Added progression domain models and objective definitions to game state.
2. Implemented deterministic objective evaluation inside domain transitions.
3. Added unlock-flag-gated building behavior (`Quarry`) to support objective chain step 5.
4. Added JSON objective loader + validation with clear fail-fast exceptions.
5. Updated projection/presentation to display objective state with command-only UI interaction.
6. Bumped persistence to schema v3 and implemented v2->v3 migration defaults.
7. Added tests for deterministic progression chain and migration fixture loading.

---

## 4) Verification

### Automated commands to run locally (Windows)
- `dotnet test`

### Godot run steps (Windows)
1. Open the project folder in Godot 4.x.
2. Run scene `scenes/main.tscn` (or run project).
3. Perform sequence:
   - Expand Tile 1
   - Place Camp on Tile 0
   - Wait ticks / click actions until objective progression advances
   - Upgrade Building 1
   - Expand Tile 2
   - Place Quarry on Tile 2 (should be rejected before unlock, accepted after unlock)
   - Save and Load
4. Confirm objective labels/progress/completion update and state persists.

### What tests assert
- `Mvp0DeterminismTests`: deterministic replay with MVP-2 command sequence.
- `Mvp0SaveRoundTripTests`: v3 save/load preserves progression and economy.
- `Mvp1DomainRulesTests`: MVP-1 behavior remains valid under progression changes.
- `Mvp2ProgressionRulesTests`: six-step chain completion and quarry unlock gating.
- `Mvp2SaveMigrationTests`: v2 fixture migrates to v3 defaults safely.

---

## 5) MVP-2 Done Criteria

MVP-2 is complete when:
1. Six-step objective chain is completable deterministically.
2. Rewards/unlocks are applied only through deterministic domain transitions.
3. Objective content is loaded from JSON with validation and clear loader errors.
4. Save schema v3 persists progression and unlock flags.
5. v2 fixture migration test passes.
6. UI only sends commands and displays projected objective/progress/completion state.

**Stop condition:** this change stops at MVP-2 and does not include MVP-3/Stage 7 work.


## Build & Test (Repository Hygiene Update)

- Solution file: `LandBuilder.sln`
- Build: `dotnet build .\LandBuilder.sln`
- List tests: `dotnet test .\LandBuilder.sln --list-tests`
- Run tests: `dotnet test .\LandBuilder.sln`

Notes:
- Production C# code is compiled from `src/LandBuilder.Core/` only.
- Tests run from `tests/LandBuilder.Tests/` and load objective/fixture files via copied content paths.
