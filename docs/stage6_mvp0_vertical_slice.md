# Stage 6 – MVP-0 Step-by-Step Build Guidance (Vertical Slice Only)

## 1) MVP-0 Target (This Stage Only)

Implement and validate the minimal end-to-end path:

**Input → Command → Domain Simulation (deterministic tick) → Events → UI Projection → Save/Load**

This stage intentionally excludes MVP-1/2/3 systems.

---

## 2) Godot + C# Project Setup (MVP-0)

## 2.1 Required Project Settings

File: `project.godot`
- `run/main_scene = res://scenes/main.tscn`
- `physics/common/physics_ticks_per_second = 30`

Reason:
- Fixed physics tick provides a stable cadence from engine side.
- Domain tick still remains isolated in `DeterministicTickScheduler`.

## 2.2 Folder Structure (Stage 2 boundaries)

- `src/Domain`
  - pure simulation types/rules/events
- `src/Application`
  - command dispatch/session/scheduler/projection
- `src/Infrastructure`
  - save/load repository and schema version stub
- `src/Presentation`
  - Godot UI/input adapter only
- `scenes/`
  - minimal scene graph
- `tests/`
  - determinism and save/load round-trip tests

## 2.3 Minimal Scene Structure

- `scenes/main.tscn`
  - root `Control`
  - labels for coins + last event message
  - buttons: expand tile, save, load

Why:
- Proves command path with smallest possible UI.
- Avoids adding any non-MVP polish widgets.

---

## 3) Files Created (Exact Paths)

## 3.1 Domain
- `src/Domain/GameState.cs`
- `src/Domain/Commands.cs`
- `src/Domain/Events.cs`
- `src/Domain/DeterministicSimulator.cs`

## 3.2 Application
- `src/Application/GameSession.cs`
- `src/Application/DeterministicTickScheduler.cs`
- `src/Application/UiProjection.cs`

## 3.3 Infrastructure
- `src/Infrastructure/SaveRepository.cs`

## 3.4 Presentation
- `src/Presentation/MainController.cs`
- `scenes/main.tscn`

## 3.5 Tests
- `tests/Mvp0DeterminismTests.cs`
- `tests/Mvp0SaveRoundTripTests.cs`

---

## 4) Key Code Snippets (MVP-0 Core)

### 4.1 Deterministic Domain Simulation

File: `src/Domain/DeterministicSimulator.cs`

```csharp
public static (GameState State, IReadOnlyList<IDomainEvent> Events) Apply(GameState state, IGameCommand command)
{
    return command switch
    {
        ExpandTileCommand expand => ApplyExpand(state, expand),
        TickCommand tick => ApplyTick(state, tick),
        _ => (state, new IDomainEvent[] { new CommandRejectedEvent("Unknown command") })
    };
}
```

### 4.2 Command Dispatch Boundary

File: `src/Application/GameSession.cs`

```csharp
public void Dispatch(IGameCommand command)
{
    var result = DeterministicSimulator.Apply(State, command);
    State = result.State;
    _eventSink.Publish(result.Events);
}
```

### 4.3 Engine-driven, Domain-isolated Tick Adapter

File: `src/Application/DeterministicTickScheduler.cs`

```csharp
public void Advance(double deltaSeconds)
{
    _accumulator += deltaSeconds;

    while (_accumulator >= _tickDurationSeconds)
    {
        _session.Dispatch(new TickCommand(1));
        _accumulator -= _tickDurationSeconds;
    }
}
```

### 4.4 UI Projection (Read-model only)

File: `src/Application/UiProjection.cs`

```csharp
public static UiProjection From(GameState state, IEnumerable<IDomainEvent> events)
{
    var map = state.World.Tiles.ToDictionary(x => x.Key, x => x.Value.Ownership);
    var last = events.LastOrDefault() switch
    {
        TileUnlockedEvent e => $"Tile {e.TileId} unlocked",
        CurrencySpentEvent e => $"Spent {e.Amount} coins",
        CommandRejectedEvent e => $"Action blocked: {e.Reason}",
        null => "Ready",
        _ => "Event processed"
    };

    return new UiProjection(state.Economy.Coins, map, last);
}
```

### 4.5 Persistence with Versioning Stub

File: `src/Infrastructure/SaveRepository.cs`

```csharp
private const int CurrentSchemaVersion = 1;

if (payload.SchemaVersion != CurrentSchemaVersion)
{
    throw new InvalidOperationException($"Unsupported schema version: {payload.SchemaVersion}");
}
```

### 4.6 Presentation Issues Commands Only

File: `src/Presentation/MainController.cs`

```csharp
private void IssueCommand(IGameCommand command)
{
    _session.Dispatch(command);
    RenderProjection();
}

GetNode<Button>("VBox/Buttons/ExpandTile1").Pressed += () => IssueCommand(new ExpandTileCommand(1));
```

---

## 5) Implementation Steps in Dependency Order

## Step 1 — Domain model + rules
Goal:
- Create authoritative state, commands, events, and deterministic command application.

Code to write:
- `GameState`, `Commands`, `Events`, `DeterministicSimulator`.

Verify:
- Expanding unlockable tile consumes coins and emits two events.
- Invalid expand emits `CommandRejectedEvent` and does not mutate state.

## Step 2 — Application orchestration
Goal:
- Route commands and isolate deterministic tick scheduler.

Code to write:
- `GameSession`, `InMemoryEventSink`, `DeterministicTickScheduler`, `UiProjection`.

Verify:
- Dispatch updates state and event sink.
- `Advance(delta)` emits deterministic tick events regardless of frame jitter.

## Step 3 — Persistence boundary
Goal:
- Save/load current MVP-0 state with schema version validation.

Code to write:
- `SaveRepository` JSON serializer/deserializer.

Verify:
- Save then load returns same coins and tile ownership.
- Unsupported schema version fails predictably.

## Step 4 — Presentation adapter and scene
Goal:
- Bind Godot UI input to commands and render projection.

Code to write:
- `MainController.cs`, `main.tscn`, `project.godot` entries.

Verify:
- Clicking Expand button dispatches command and updates labels.
- Save and Load buttons persist and restore current state.

## Step 5 — MVP-0 tests
Goal:
- Lock deterministic and persistence behavior.

Code to write:
- `Mvp0DeterminismTests.cs`
- `Mvp0SaveRoundTripTests.cs`

Verify:
- Same command list yields identical final state.
- Save/load round-trip preserves economy and tile ownership.

---

## 6) MVP-0 Test Plan

## 6.1 Automated

1. Determinism test:
- `tests/Mvp0DeterminismTests.cs`
- same inputs => same outputs.

2. Save/load round-trip test:
- `tests/Mvp0SaveRoundTripTests.cs`
- persisted state equals loaded state.

## 6.2 Manual QA Checklist (minimal)

- [ ] App boots into `main.tscn` with labels/buttons visible.
- [ ] Expand button unlocks tile 1 when enough coins are available.
- [ ] Coins decrease by tile cost after successful expand.
- [ ] Repeating expand on already unlocked tile shows blocked action message.
- [ ] Save then load restores current coins and tile ownership.
- [ ] No direct UI code path mutates domain state without command dispatch.

---

## 7) MVP-0 Done Criteria (Gate to MVP-1)

MVP-0 is complete only when all are true:

1. End-to-end slice works: input → command → deterministic domain simulation → events → UI projection → save/load.
2. Architecture boundaries are respected:
   - Domain has no Godot dependency.
   - Presentation does not directly mutate domain state.
3. Deterministic tick adapter is isolated from frame update logic.
4. Determinism test and save/load round-trip test are implemented and passing in CI-capable environment.
5. Manual QA checklist is completed with no blocker defects.

**Status:** MVP-0 guidance complete. Stop here before MVP-1.
