# Stage 6 – MVP-1 Build (Core Playable Loop)

## 1) MVP-1 Scope (Mapped to Stage 5 MVP-1)

### In Scope (implemented)
- One buildable building type (`Camp`) on unlocked tile(s).
- Building placement command with validation and spend cost.
- Building upgrade command (up to level 3) with cost validation.
- Deterministic tick-based coin production from placed buildings.
- UI projection updates showing coins, building count, and production/tick.
- Save/load extended to persist buildings and next building id.
- Determinism + domain-rule + save/load tests extended for new behavior.

### Out of Scope (intentionally not implemented)
- Additional building types (`Quarry`, `Workshop`) from Stage 4 placeholder set.
- Objective/progression chain (belongs to MVP-2 per Stage 5).
- Advanced balancing tools, polish VFX/audio, multi-region content.

### Stage 4/5 mismatch call-out and decision
- Stage 4 placeholder content suggests up to 3 building types, but Stage 5 MVP-1 acceptance only requires at least one deterministic producer and invalid-action handling.
- For smallest compliant scope, MVP-1 implements **one** building type only.

---

## 2) Files Changed

### Domain
- `src/Domain/GameState.cs`
- `src/Domain/Commands.cs`
- `src/Domain/Events.cs`
- `src/Domain/DeterministicSimulator.cs`

### Application
- `src/Application/DeterministicTickScheduler.cs`
- `src/Application/UiProjection.cs`

### Infrastructure
- `src/Infrastructure/SaveRepository.cs`

### Presentation
- `src/Presentation/MainController.cs`
- `scenes/main.tscn`

### Tests
- `tests/Mvp0DeterminismTests.cs` (extended for MVP-1 commands)
- `tests/Mvp0SaveRoundTripTests.cs` (extended for buildings + schema)
- `tests/Mvp1DomainRulesTests.cs` (new)

### Docs
- `docs/stage6_mvp1_build.md`
- `README.md` (added link)

---

## 3) Implementation Steps (Dependency Order)

1. Extend domain state to include building instances and next-id counter.
2. Add placement/upgrade commands and corresponding domain events.
3. Add deterministic production in `TickCommand` handling.
4. Update UI projection model to include building and production stats.
5. Update presentation controls to issue new commands only.
6. Expand save schema to v2 and add v1->v2 migration stub defaults.
7. Extend tests for determinism and persistence; add domain rule tests.

---

## 4) Verification

Expected runtime checks:
- Place Camp on tile 0 decreases coins by 12 and shows building count 1.
- Tick updates increase coins deterministically.
- Upgrade building 1 spends coins and increases production rate.
- Invalid placement on occupied tile returns blocked message.
- Save then load restores buildings, coins, and schema v2 state.

---

## 5) MVP-1 Done Criteria

MVP-1 is complete when all are true:
1. Player can place at least one producer building and earn coins from deterministic ticks.
2. Player can upgrade a building and see increased production.
3. Invalid building actions are blocked via command rejection events.
4. UI remains projection-only and issues commands exclusively.
5. Save/load round-trip preserves building state and economy under schema v2.
6. Determinism tests include MVP-1 commands and pass in a .NET-enabled environment.

**Stop condition:** Work stops at MVP-1. No MVP-2/MVP-3 features included.
