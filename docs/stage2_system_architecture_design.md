# Stage 2 – System Architecture Design

## 1) Architecture Goals

This architecture is designed to support a **data-driven incremental builder game** with stable long-term maintainability.

Primary goals:
- Deterministic simulation core (economy/progression correctness).
- Decoupled systems (UI and content changes should not break simulation).
- Content scalability (new tiles/buildings/regions via data, minimal code changes).
- Safe persistence with save versioning/migrations.
- Tooling hooks for balancing and debugging.

Non-goals for this stage:
- Final art pipeline details.
- Engine-specific implementation syntax.

---

## 2) High-Level Module Breakdown

## 2.1 Core Runtime Modules

1. **Game Orchestrator**
   - Bootstraps subsystems.
   - Owns game states (Boot, MainMenu, Loading, InGame, Paused).
   - Coordinates frame/tick scheduling.

2. **World/Map System**
   - Stores tile graph/grid and tile states (locked/unlockable/unlocked).
   - Validates expansion adjacency and tile ownership transitions.
   - Exposes spatial queries for build placement.

3. **Building System**
   - Handles placement/removal/upgrade of buildings.
   - Resolves tile/building compatibility constraints.
   - Supplies production capabilities to economy simulation.

4. **Economy Simulation**
   - Runs deterministic resource production/consumption ticks.
   - Applies multipliers (global, local, temporary buffs).
   - Handles transactions (spend, reward, conversion).

5. **Progression System**
   - Tracks objectives, milestone conditions, unlock flags.
   - Grants rewards and unlocks systems/content.
   - Provides “next actionable goals” to UI.

6. **Input & Interaction System**
   - Maps desktop controls to game actions.
   - Converts pointer world-space selection to intent events.
   - Manages interaction modes (inspect/build/expand).

7. **UI Presentation Layer**
   - Renders HUD and panels from read-model data.
   - Sends user intents to orchestrator/command handlers.
   - No direct mutation of simulation state.

8. **Persistence System**
   - Save/load/autosave profile state.
   - Save schema versioning and migration execution.
   - Handles offline progress delta application on load.

9. **Feedback System (Audio/VFX/Notifications)**
   - Subscribes to domain events.
   - Plays visual/audio responses.
   - Maintains event-to-feedback mapping in data configs.

10. **Telemetry/Debug Tools**
   - Economy graphs, tick diagnostics, object inspectors.
   - Debug commands (grant resources, time scale, unlock all).

---

## 3) Interaction Topology (How modules communicate)

### 3.1 Core principle: Command + Event flow

- **Commands** mutate authoritative game state.
- **Domain events** are emitted after successful mutations.
- **Read models** are derived for UI consumption.

Flow:
1. Input/UI creates command (`ExpandTile`, `PlaceBuilding`, `UpgradeBuilding`).
2. Orchestrator routes command to target domain system.
3. Domain validates preconditions and mutates state.
4. Domain emits event(s) (`TileUnlocked`, `CurrencySpent`, `ObjectiveCompleted`).
5. Dependent systems react (progression, feedback, telemetry, UI projections).

### 3.2 Coupling rules

- UI depends on read models only.
- Feedback never writes authoritative gameplay state.
- Economy never calls UI directly.
- Progression listens to events rather than hard-calling other systems.

---

## 4) Proposed Design Patterns

1. **Entity-component-lite for world objects**
   - Keep lightweight data components on tiles/buildings.
   - Avoid deep inheritance for content variants.

2. **Command Handlers**
   - Each player/system action represented as explicit command struct/object.
   - Centralized validation and transactional mutation.

3. **Event Bus (in-process)**
   - Publish/subscribe domain events.
   - Supports loose coupling and easier debugging.

4. **State Machine**
   - Game flow states (menu/loading/in-game/pause).
   - Interaction mode states (inspect/build/expand).

5. **Repository/Service boundary for persistence**
   - Save repository handles serialization IO.
   - Domain services remain serialization-agnostic.

6. **Data-driven content registry**
   - Building/tile/upgrade definitions loaded from data assets (JSON/TOML/engine resources).

---

## 5) Data Model Blueprint

## 5.1 Runtime Domain Models

### WorldState
- `worldId: String`
- `tiles: Map<TileId, TileState>`
- `discoveredFrontier: Set<TileId>`
- `placedBuildings: Map<BuildingInstanceId, BuildingState>`

### TileState
- `tileId: Int`
- `terrainType: Enum`
- `ownership: Locked | Unlockable | Unlocked`
- `adjacentTileIds: List<Int>`
- `buildSlotCount: Int`
- `placedBuildingIds: List<BuildingInstanceId>`

### EconomyState
- `currencies: Map<CurrencyId, Decimal>`
- `productionRates: Map<CurrencyId, DecimalPerSecond>`
- `globalMultipliers: Map<MultiplierId, Float>`
- `transactionLog: RingBuffer<TransactionEvent>`

### BuildingState
- `instanceId: UUID`
- `buildingTypeId: String`
- `tileId: Int`
- `level: Int`
- `active: Bool`

### ProgressionState
- `activeObjectives: List<ObjectiveProgress>`
- `completedObjectives: Set<ObjectiveId>`
- `unlockFlags: Set<UnlockFlag>`
- `chapterId: String`

### MetaState
- `elapsedGameTimeSeconds: Long`
- `lastSaveUtc: Timestamp`
- `schemaVersion: Int`

## 5.2 Static Content Models (Data Assets)

### TileDefinition
- Terrain constraints.
- Base unlock cost formula reference.
- Biome/region tags.

### BuildingDefinition
- Placement rules (allowed terrain, adjacency requirements).
- Base production profile.
- Upgrade curve references.
- Unlock prerequisites.

### ObjectiveDefinition
- Requirement predicates.
- Reward payloads.
- Dependency graph (objective chains).

### EconomyCurveDefinition
- Cost curve functions/parameters.
- Multiplier sources and stacking rules.

---

## 6) State Flow and Tick Lifecycle

## 6.1 Frame phases (InGame)

1. **Input Phase**
   - Collect raw input.
   - Generate intents and commands.
2. **Command Phase**
   - Validate and execute command handlers.
   - Emit domain events.
3. **Simulation Tick Phase** (fixed-step)
   - Economy production/consumption tick.
   - Time-based progression checks.
   - Emit periodic events (`ResourceProduced`, `ObjectiveProgressed`).
4. **Projection Phase**
   - Update UI read models from current state.
5. **Feedback Phase**
   - Execute VFX/SFX/notifications from event queue.
6. **Autosave Check Phase**
   - Run periodic save policy (e.g., every N seconds + key actions).

## 6.2 Determinism policy

- Use fixed simulation step for economy math.
- Keep mutation order stable and explicit.
- Avoid frame-rate-coupled economy updates.

---

## 7) Folder Structure and Project Layout (engine-agnostic)

```text
Land-Builder/
  docs/
    stage1_game_analysis_scope.md
    stage2_system_architecture_design.md
  src/
    app/
      game_orchestrator.*
      game_state_machine.*
    domain/
      world/
        world_state.*
        tile_state.*
        world_commands.*
        world_events.*
      buildings/
        building_state.*
        building_commands.*
        building_events.*
      economy/
        economy_state.*
        economy_simulator.*
        transaction_model.*
      progression/
        objective_state.*
        objective_evaluator.*
        unlock_manager.*
      shared/
        ids.*
        value_types.*
        domain_event_bus.*
    application/
      command_router.*
      read_model_projectors.*
      gameplay_facade.*
    infrastructure/
      persistence/
        save_repository.*
        save_schema_v*.*/
        migrations/
      content/
        content_loader.*
        validators/
      logging/
        telemetry_logger.*
    presentation/
      ui/
        hud/
        panels/
        notifications/
      input/
        input_mapper.*
        interaction_modes.*
      feedback/
        vfx_controller.*
        sfx_controller.*
    tools/
      debug/
        console_commands.*
        economy_overlay.*
  data/
    tiles/
    buildings/
    objectives/
    economy/
  tests/
    domain/
    application/
    integration/
```

Layout policy:
- `domain` has no dependency on presentation/infrastructure.
- `application` coordinates use-cases and projections.
- `infrastructure` handles IO/data loading.
- `presentation` consumes read models and emits commands.

---

## 8) Save/Load Architecture and Versioning

### 8.1 Save payload partitions
- `meta` (profile metadata, version, timestamps)
- `world` (tile/building ownership/placement)
- `economy` (currencies, rates, modifiers)
- `progression` (objectives, unlock flags)
- `settings` (audio/video/input)

### 8.2 Versioning strategy
- Increment integer schema version for each breaking save model change.
- Define migration chain `vN -> vN+1`.
- Refuse load only when migration impossible; otherwise auto-migrate and re-save.

### 8.3 Offline progress integration
On load:
1. Compute elapsed real time since `lastSaveUtc`.
2. Clamp to configured max offline window.
3. Simulate aggregated production via deterministic economy function.
4. Emit `OfflineRewardGranted` event and present summary panel.

---

## 9) Error Handling and Observability

1. **Validation errors** (user-action invalid)
   - Return structured error code; show user-friendly UI feedback.
2. **Domain invariant errors** (logic bugs)
   - Log high severity with state snapshot; fail fast in debug builds.
3. **Infrastructure errors** (save IO/content parsing)
   - Retry policy where safe.
   - Fallback to backup save slot on corruption.

Observability requirements:
- Event trace logging toggle.
- Economy time-series sampling.
- Command execution timing metrics.

---

## 10) Scalability and Maintainability Rules

1. Add new building/tile via data definitions first; code changes only for novel mechanics.
2. No direct UI mutation from domain systems.
3. Every command handler must declare validation checks and emitted events.
4. Every persistent model change requires:
   - schema version bump,
   - migration,
   - regression load test.
5. Keep simulation and rendering separate for future performance scaling.

---

## 11) Architecture Milestone Checklist (Stage 2 completion)

Stage 2 is complete when:

- Module boundaries and responsibilities are defined.
- Communication pattern (commands/events/read models) is fixed.
- Data models for runtime and content are documented.
- Tick/state flow is specified with determinism policy.
- Folder/layout structure and dependency direction are documented.
- Save versioning/migration strategy is defined.
- Error handling and observability requirements are listed.

**Status:** Complete. Ready to proceed to Stage 3 (Technology Stack and Tooling) in the next step.
