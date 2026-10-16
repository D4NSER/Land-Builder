# Stage 5 – Implementation Roadmap

## 1) Roadmap Alignment Baseline

This implementation roadmap is explicitly aligned with:

- **Stage 2 Architecture**
  - Domain/Application/Infrastructure/Presentation boundaries are preserved.
  - Deterministic fixed-step simulation is mandatory for economy/progression logic.
  - Command → handler → domain events → read-model projection flow is enforced.
- **Stage 3 Stack**
  - Godot 4.x + C# (.NET 8), with domain code engine-agnostic.
  - CI and testing strategy integrated from the start.
- **Stage 4 MVP**
  - Milestones map directly to MVP-0 through MVP-3.
  - No out-of-scope features included.

Planning-only note:
- This document defines implementation sequencing and quality gates only; it does not start coding.

---

## 2) Vertical Slice Order (First End-to-End Loop)

## 2.1 First Vertical Slice (implemented first)

Minimal loop, end-to-end, in this strict order:
1. **Input**: click tile in Expand mode.
2. **Application command**: `ExpandTileCommand` dispatched.
3. **Domain simulation**: adjacency + funds validation, spend coins, unlock tile.
4. **Domain events**: `CurrencySpent`, `TileUnlocked` emitted.
5. **UI projection**: HUD + tile state update from read model.
6. **Persistence**: manual save then reload restores unlocked tile and coin balance.

This is the earliest proof that all architecture layers are connected correctly.

## 2.2 Incremental Expansion of Vertical Slice

After first slice:
- Add building placement and deterministic production tick.
- Add objective progression reacting to emitted events.
- Add autosave and stability pass.

---

## 3) Milestone-to-Phase Map

- **Phase A → MVP-0:** Foundation + architecture skeleton + first command/event flow.
- **Phase B → MVP-1:** Core loop playable (expand/build/produce).
- **Phase C → MVP-2:** Progression + persistence reliability.
- **Phase D → MVP-3:** Usability, stabilization, determinism hardening.

Each phase below includes objective, scope, modules, task order, tests, risks, and complexity.

---

## 4) Phase A (MVP-0): Foundation and Vertical Slice Scaffolding

### Objective
Establish architecture skeleton and prove the first end-to-end command/event/presentation/save path.

### Scope
Included:
- Project structure by layer.
- Core command/event bus plumbing.
- Minimal map render + tile selection + one expand action.
- Manual save/load for minimal state.

Excluded:
- Building production loop.
- Objective chain.
- Advanced UI polish.

### Components/Modules Affected
- `Domain`: world state, tile state, expand validation.
- `Application`: command router, read-model projector.
- `Infrastructure`: basic save repository.
- `Presentation (Godot)`: input mapper, tile view, basic HUD.

### Task Sequence (Dependency Order)
1. Create solution/projects and layer references (enforce directionality).
2. Implement core IDs/value objects and base event bus in Domain.
3. Implement `ExpandTileCommand` + handler in Application.
4. Implement world unlock rule checks in Domain.
5. Implement read model projection (tile state + coin display).
6. Wire Godot input to command dispatch.
7. Implement minimal JSON save/load for world + currency.
8. Add first smoke test and deterministic command replay fixture.

### Acceptance Criteria
- Tile can be selected and expanded through command flow.
- Currency decreases deterministically on expand.
- Event trace shows `CurrencySpent` then `TileUnlocked`.
- Save/load restores expanded tile + currency exactly.

### Risks and Mitigations
- Risk: architecture leakage into Godot scripts.
  - Mitigation: keep command/domain logic out of scene scripts; review dependency graph.
- Risk: early save schema drift.
  - Mitigation: include `schemaVersion` from first save format.

### Complexity
- **M**

### Test Plan (Phase A)
- Unit:
  - adjacency validation rule tests,
  - insufficient funds test.
- Integration:
  - command→event sequence test,
  - save/load round-trip for minimal state.
- Manual QA:
  - click-to-expand responsiveness,
  - invalid expand shows feedback,
  - reload preserves expansion.

---

## 5) Phase B (MVP-1): Core Loop Playable (Expand + Build + Produce)

### Objective
Deliver first true playable loop with tile expansion, building placement, and coin production.

### Scope
Included:
- Building definitions (3 MVP placeholders).
- Placement validation and slot constraints.
- Fixed-step economy tick and production accumulation.
- Upgrade path (levels 1–3).

Excluded:
- Objective progression chain.
- Autosave reliability pass.
- Broad balancing iteration beyond sanity targets.

### Components/Modules Affected
- `Domain`: building state/rules, economy simulator.
- `Application`: place/upgrade command handlers, production projection.
- `Infrastructure`: data loaders for building definitions.
- `Presentation`: build mode UI, building context panel, production HUD.

### Task Sequence (Dependency Order)
1. Add building definition schema + loader validation.
2. Implement `PlaceBuildingCommand` and validation rules.
3. Implement deterministic economy tick service.
4. Connect building production output to coin balance.
5. Implement `UpgradeBuildingCommand` with cost + output scaling.
6. Add HUD fields for current coins and coins/min.
7. Add feedback for successful place/upgrade and invalid attempts.
8. Add tests for production math and command constraints.

### Acceptance Criteria
- Player can place at least one building on valid tile and earn coins.
- Upgrading increases production according to formula.
- Economy result for fixed tick replay is stable across runs.
- Invalid placements are blocked and surfaced in UI.

### Risks and Mitigations
- Risk: frame-rate coupled production.
  - Mitigation: enforce fixed tick loop in Application/Domain, never frame delta.
- Risk: production balancing instability.
  - Mitigation: expose config-driven constants and short tuning loop.

### Complexity
- **L**

### Test Plan (Phase B)
- Unit:
  - placement rule matrix,
  - upgrade cost/output formula tests,
  - per-tick production accumulation.
- Integration:
  - place→tick→currency update path,
  - deterministic replay over N ticks.
- Manual QA:
  - build mode clarity,
  - coins visibly increasing at expected rate,
  - upgrade feedback readability.

---

## 6) Phase C (MVP-2): Progression + Save/Load Reliability

### Objective
Add progression structure and make persistence robust enough for repeatable play sessions.

### Scope
Included:
- Objective chain (minimum 6 steps per Stage 4).
- Unlock flags and reward distribution.
- Autosave triggers + manual save/load UX.
- Save schema migration scaffold and compatibility tests.

Excluded:
- Offline reward system.
- Multi-region progression.

### Components/Modules Affected
- `Domain`: objective predicates, unlock flags, progression state.
- `Application`: objective evaluation subscribers, reward dispatch.
- `Infrastructure`: save partitions, migration chain, fixture loading.
- `Presentation`: objective tracker panel, completion notifications.

### Task Sequence (Dependency Order)
1. Add objective definition schema and parser.
2. Implement progression state and objective evaluator.
3. Subscribe objective evaluator to domain events.
4. Implement reward payload execution (coins/unlocks).
5. Expand save model partitions (`meta/world/economy/progression/settings`).
6. Add autosave policy and safe sync points.
7. Implement migration interface and first no-op migration.
8. Add compatibility fixtures and persistence regression tests.

### Acceptance Criteria
- Six-step objective chain is completable in one run.
- Objective completion correctly unlocks gated content/actions.
- Reload restores objective and unlock states exactly.
- Autosave + manual save both produce valid loadable files.

### Risks and Mitigations
- Risk: event subscription ordering bugs in objective evaluation.
  - Mitigation: deterministic event processing order and integration assertions.
- Risk: save corruption or incompatible schema updates.
  - Mitigation: backup slot + migration test suite on CI.

### Complexity
- **L**

### Test Plan (Phase C)
- Unit:
  - objective predicate evaluation,
  - unlock flag state transitions,
  - reward payload validation.
- Integration:
  - event-driven progression flow,
  - autosave/manual-save round-trip,
  - migration fixture load test.
- Manual QA:
  - objective tracker clarity,
  - progression pacing feels coherent,
  - reload continuity after objective completion.

---

## 7) Phase D (MVP-3): Usability Pass and Stabilization

### Objective
Harden the MVP for stability, determinism confidence, and desktop usability before Stage 6 guided build execution.

### Scope
Included:
- Input usability pass (hotkeys, camera, cancel flow).
- HUD/notification consistency and readability updates.
- Determinism stress tests and bug fixing.
- Smoke test matrix for Windows/Linux/macOS builds.

Excluded:
- Non-MVP polish systems (advanced VFX/audio, localization, extra regions).

### Components/Modules Affected
- `Presentation`: input map tuning, UI usability refinements.
- `Application/Domain`: determinism and ordering fixes.
- `Infrastructure`: logging/telemetry and packaging scripts.

### Task Sequence (Dependency Order)
1. Run structured playtests, log top UX friction points.
2. Implement high-impact input and HUD improvements.
3. Add/extend determinism replay suites (long-run scenarios).
4. Fix desync/state drift defects.
5. Run cross-platform CI export smoke builds.
6. Execute final MVP bug triage and priority fixes.

### Acceptance Criteria
- 30-minute play session without blocker issues.
- Determinism replay suite passes consistently.
- Cross-platform artifacts build successfully in CI.
- No critical save/load defects remain open.

### Risks and Mitigations
- Risk: late stabilization reveals deep architecture defects.
  - Mitigation: strict milestone gates and earlier integration tests.
- Risk: platform-specific packaging failures.
  - Mitigation: incremental CI matrix from early milestones, not end-only.

### Complexity
- **M/L**

### Test Plan (Phase D)
- Unit:
  - regression tests for previously fixed rules.
- Integration:
  - long-horizon deterministic simulation replay,
  - end-to-end save/load under repeated cycles.
- Manual QA:
  - desktop control comfort,
  - visibility/readability of game state,
  - progression “feel” and frustration check.

---

## 8) Cross-Milestone Quality Gates

Apply to every phase:
- All new commands have validation tests.
- All domain events affecting progression/economy have integration assertions.
- No Presentation-to-Domain direct mutation.
- Save schema updates include migration and fixture tests.
- CI must pass lint/format/tests before merge.

---

## 9) Repository and Workflow Plan

## 9.1 Branch Strategy
- `main`: protected, release-ready.
- `develop` (optional): integration branch for milestone aggregation.
- Feature branches from `main` or `develop`:
  - `feature/mvp0-command-flow`
  - `feature/mvp1-economy-tick`
  - `feature/mvp2-save-migrations`
  - `feature/mvp3-ux-stabilization`

Hotfix branches:
- `hotfix/<short-description>`

## 9.2 PR Granularity Guidelines
- Keep PRs vertical and testable (one meaningful system increment).
- Preferred PR size:
  - 200–600 LOC net for logic-heavy changes,
  - larger allowed only for generated assets/config additions with review notes.
- One PR should not span unrelated layers unless required for vertical slice completion.
- Every PR must include:
  - architecture boundary note,
  - test evidence,
  - risk note (if touching determinism or persistence).

## 9.3 Naming Conventions

Task IDs (suggested):
- `LB-MVP0-###`, `LB-MVP1-###`, etc.

Branch names:
- `feature/lb-mvp1-place-building`
- `feature/lb-mvp2-objective-evaluator`

PR titles:
- `[MVP-1] Add deterministic production tick service`
- `[MVP-2] Implement save partition migration scaffolding`

Commit message style:
- `mvp1: add upgrade command validation`
- `mvp2: add schema version and migration interface`

---

## 10) Recommended Backlog Breakdown by Milestone

### MVP-0 Backlog Seeds
- Solution scaffolding and layer contracts.
- Domain core primitives (IDs, value objects, event bus).
- Expand tile vertical slice.
- Minimal save/load path.

### MVP-1 Backlog Seeds
- Building definition loader + validation.
- Placement and upgrade commands.
- Deterministic economy tick.
- Build mode UI and production HUD.

### MVP-2 Backlog Seeds
- Objective evaluator and unlock manager.
- Reward dispatch pipeline.
- Save partition expansion + autosave.
- Migration fixtures and compatibility tests.

### MVP-3 Backlog Seeds
- Input/UX refinement tasks.
- Determinism long-run test suite.
- Cross-platform export automation.
- Stabilization bug bash and triage.

---

## 11) Stage 5 Exit Criteria (before Stage 6 starts)

Stage 5 is complete only when:

1. All MVP phases (A–D) are defined and mapped to MVP-0..MVP-3.
2. Each phase has objective, scope boundaries, affected modules, ordered tasks, acceptance criteria, risk/mitigation, and complexity estimate.
3. Vertical slice order is documented from input → simulation → UI → save/load.
4. Per-milestone test plans exist across unit/integration/manual QA.
5. Repository workflow (branching, PR granularity, naming conventions) is defined.
6. Quality gates are listed for determinism, architecture boundaries, and persistence safety.
7. Team agrees that Stage 6 can execute phase-by-phase without redefining roadmap scope.

**Status:** Complete. Ready to proceed to Stage 6 (Step-by-Step Build Guidance) in the next step.
