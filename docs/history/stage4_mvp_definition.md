# Stage 4 – Minimum Viable Product Definition

## 1) MVP Goal

Build the **smallest playable desktop version** that proves the Land-Builder core loop end-to-end:

1. Expand land tiles.
2. Place/upgrade production buildings.
3. Earn and spend currency through a deterministic tick simulation.
4. Complete simple objectives that gate progression.
5. Save and load progress reliably.

Success definition:
- A player can start a new game, reach a stable positive economy, unlock additional map space, complete a short objective chain, save, reload, and continue without state corruption.

---

## 2) MVP Scope Boundaries

## 2.1 In Scope (MVP Inclusions)

- Single playable region/map (small curated layout).
- One primary currency (`Coins`).
- Tile unlocking with adjacency rules.
- 2–3 building types with level upgrades.
- Deterministic fixed-step economy tick.
- Basic objective chain (tutorial-like progression steps).
- Desktop-first controls (mouse + key shortcuts for mode switching).
- Minimal but functional HUD and context panels.
- Autosave + manual save/load with schema versioning.
- Basic feedback (highlights, toasts, simple SFX trigger points).

## 2.2 Out of Scope (Explicit Exclusions)

- Multiple regions/biomes.
- Multi-resource crafting/conversion chains.
- Offline idle rewards.
- Advanced VFX/audio polish.
- Accessibility feature set (full remapping, colorblind presets, etc.)
- Localization and narrative content layers.
- Achievements, meta progression, prestige, endgame loops.
- Full balancing tools beyond minimal debug overlays.

---

## 3) MVP Feature Checklist by System

## 3.1 World/Map
- [ ] Grid/tile map loaded from data.
- [ ] Tile states: `Locked`, `Unlockable`, `Unlocked`.
- [ ] Adjacency validation for unlock attempts.
- [ ] Unlock cost lookup and transaction trigger.

## 3.2 Interaction
- [ ] Selection/hover for tiles/buildings.
- [ ] Modes: `Inspect`, `Expand`, `Build`.
- [ ] Command dispatch for expand/place/upgrade.
- [ ] Clear invalid-action feedback.

## 3.3 Economy
- [ ] Fixed-step simulation tick (deterministic order).
- [ ] One soft currency balance (`Coins`).
- [ ] Production per building per tick.
- [ ] Spend transactions for tile unlock and upgrades.

## 3.4 Buildings
- [ ] Building definition data loaded at startup.
- [ ] Placement validation (allowed tiles + slot limits).
- [ ] 3-level upgrade path per building.
- [ ] Production scaling by building level.

## 3.5 Progression
- [ ] Objective definition set loaded from data.
- [ ] Sequential objective tracking (active/completed).
- [ ] Unlock flags that gate next actions/content.
- [ ] Objective reward dispatch (coins/unlock).

## 3.6 UI
- [ ] HUD: currency, production rate, active objective.
- [ ] Context panel for selected tile/building.
- [ ] Build panel filtered by unlock rules.
- [ ] Notification toasts for milestones/errors.

## 3.7 Persistence
- [ ] Save partition model (`meta/world/economy/progression/settings`).
- [ ] Manual save and load actions.
- [ ] Autosave timer and key-event triggers.
- [ ] Save schema version field and migration hook scaffold.

## 3.8 Feedback
- [ ] Tile highlight states (hover/selected/invalid).
- [ ] Simple positive feedback on purchase/upgrade.
- [ ] Objective completion toast/banner.
- [ ] Failure feedback for insufficient funds/invalid placement.

---

## 4) Milestone Plan and Acceptance Criteria

## MVP-0: Project Skeleton + Vertical Slice Scaffolding
Goal:
- Establish architecture-aligned project structure and data wiring without full gameplay depth.

Deliverables:
- Base project structure (`domain/application/infrastructure/presentation`).
- Command/event plumbing operational with stub handlers.
- Simple map render with selectable tiles.

Acceptance criteria:
- App boots to playable scene.
- Selecting a tile updates UI context panel.
- At least one command executes and emits a traceable event.

## MVP-1: Core Loop Playable (Expand + Build + Produce)
Goal:
- First true playable loop.

Deliverables:
- Tile unlock with adjacency + coin cost.
- Building placement and production tick.
- Currency increases over time and is spendable.

Acceptance criteria:
- Player can unlock at least 5 additional tiles in one session.
- At least one building type generates coins deterministically.
- Invalid actions (no funds/invalid tile) are blocked with feedback.

## MVP-2: Progression + Save/Load Reliability
Goal:
- Make loop persist and progression-driven.

Deliverables:
- Objective chain (minimum 6 steps).
- Unlock flags connected to objectives.
- Save/load/autosave path complete.

Acceptance criteria:
- New game to “mini completion” path is finishable in ~15–30 minutes.
- Reload preserves tile, building, economy, and objective state exactly.
- Save schema version is written and load path handles matching version.

## MVP-3: Usability Pass + Stabilization
Goal:
- Desktop usability and stability for handoff to Stage 5 roadmap execution.

Deliverables:
- Input polish (camera zoom/pan, mode shortcuts).
- HUD clarity improvements and notification consistency.
- Determinism and regression test pass.

Acceptance criteria:
- 30-minute playtest session has no blocker bugs.
- Determinism test scenario yields identical results across repeated runs.
- No critical save/load bugs in smoke test matrix.

---

## 5) Required Placeholder Content (Minimum Data Set)

### 5.1 Tiles/Map
- 1 region.
- 36 total tiles (6x6 equivalent layout).
- 6 initially unlocked tiles.
- 30 unlockable via adjacency.
- Terrain types used in MVP:
  - `Grass` (general buildable),
  - `Rocky` (restricted to specific building),
  - `WaterEdge` (non-buildable placeholder for constraints).

### 5.2 Buildings
Minimum 3 building types:
1. `Camp` (basic low-output coin producer, buildable on Grass).
2. `Quarry` (medium output, Rocky-only constraint).
3. `Workshop` (higher cost/output, unlocks after early objectives).

For each building:
- Levels: 1–3.
- Cost progression formula.
- Output progression formula.

### 5.3 Objectives
Minimum 6-step objective chain:
1. Unlock first adjacent tile.
2. Place first Camp.
3. Reach X coins earned cumulatively.
4. Upgrade Camp to level 2.
5. Unlock Rocky tile and place Quarry.
6. Reach mini-economy threshold (e.g., Y coins/min) to complete MVP path.

### 5.4 UI Text/Labels
- Placeholder but explicit labels for action buttons and objective prompts.
- Error message placeholders for validation failures.

---

## 6) Biggest MVP Risks and Mitigations

## 6.1 Desktop UX Friction
Risk:
- Mobile-inspired interactions feel clumsy with mouse/keyboard if not adapted.

Mitigation:
- Prioritize hover states, right-click cancel behavior, and mode hotkeys.
- Run early usability playtests (internal) before content expansion.

## 6.2 Determinism Drift
Risk:
- Inconsistent simulation outcomes due to frame-dependent updates or float accumulation.

Mitigation:
- Fixed-step tick only for economy.
- Deterministic processing order for entities/events.
- Determinism regression test with replayed command sequence.

## 6.3 Save/Load Instability
Risk:
- State mismatch or corruption when reloading during active progression.

Mitigation:
- Partitioned save schema with version field.
- Serialization round-trip tests and fixture checks.
- Save only at safe sync points for autosave.

## 6.4 Scope Creep During MVP
Risk:
- Pulling in secondary systems before core loop is stable.

Mitigation:
- Hard feature gate using in-scope/out-of-scope list.
- Any new feature requires explicit post-MVP tagging.

## 6.5 Balance Collapse
Risk:
- Economy either stalls or snowballs too quickly, invalidating gameplay proof.

Mitigation:
- Start with simple controllable formulas.
- Use debug overlay for PPM/SPM tracking during test runs.
- Iterate using short playtest loops and fixed balancing checkpoints.

---

## 7) Stage 4 Exit Criteria (before Stage 5 starts)

Stage 4 is complete only when all are true:

1. MVP goal is explicitly documented and agreed.
2. In-scope vs out-of-scope feature boundaries are fixed.
3. System-level MVP checklist exists and is implementation-ready.
4. Milestones (MVP-0 → MVP-3) have concrete acceptance criteria.
5. Placeholder content pack is defined (tiles/buildings/objectives/UI text).
6. Critical risks (desktop UX, determinism, save/load, scope) have mitigation plans.
7. Team confirms no Stage 5 implementation roadmap work starts before this gate.

**Status:** Complete. Ready to proceed to Stage 5 (Implementation Roadmap) in the next step.
