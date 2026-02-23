# Stage 3 – Technology Stack and Tooling

## Recommended Default Stack

### Primary Engine/Framework: **Godot 4.3+ (2D workflow), C# scripting**

Why this is the best default for this project:
- Strong cross-platform desktop exports (Windows/Linux/macOS) out of the box.
- Excellent 2D tooling for tile/grid/island-style builder gameplay.
- C# support enables maintainable architecture with clear boundaries matching Stage 2.
- Scene/node workflow works well for Presentation while keeping Domain logic pure.
- Open-source and low tooling friction for an educational, long-running production project.

Stage 2 compatibility:
- Deterministic simulation can run in a fixed-step domain tick loop independent of rendering.
- Domain/Application code can be plain C# assemblies with no Godot dependency.
- Godot layer serves as adapter for input, rendering, UI, and platform services.
- Command/Event boundaries map naturally to command handlers + event bus in C# services.

---

## Viable Alternatives and Trade-offs

### Alternative A: **Unity (2022 LTS/6000 LTS), C#**
Pros:
- Mature editor ecosystem and strong profiling/debugging.
- Broad hiring/community familiarity.
- Solid desktop build pipeline and package ecosystem.

Cons:
- Overhead and complexity for architecture purity unless carefully enforced.
- Licensing and ecosystem churn concerns compared to Godot for educational projects.
- Tendency for logic to leak into MonoBehaviours without strong discipline.

Best if:
- Team already has deep Unity experience and wants rapid tooling integration.

### Alternative B: **Bevy (Rust)**
Pros:
- Strong ECS architecture and performance potential.
- Excellent for deterministic simulation and strict modular boundaries.
- Great long-term technical learning value.

Cons:
- Smaller tooling/content pipeline for 2D production compared to Godot/Unity.
- UI workflow and iteration speed typically slower for non-Rust teams.
- Higher onboarding cost.

Best if:
- Primary project goal is systems programming + ECS mastery.

---

## Programming Language Selection

### Default: **C# 12 / .NET 8**

Rationale:
- Aligns with Stage 2 architecture layering (Domain/Application/Infrastructure/Presentation).
- Strong typing and tooling for complex game-state models.
- Mature testing ecosystem (xUnit/NUnit, snapshot/serialization tests).
- Smooth interop with Godot C# and desktop platform libraries.

Language policy:
- Domain and Application projects remain engine-agnostic C# class libraries.
- Presentation layer references Godot APIs.
- Infrastructure implements repositories, file IO, config loading, migrations.

---

## Rendering Approach and Asset Pipeline

### Rendering Strategy
- 2D orthographic camera.
- Tilemap-based world rendering with layered tile sets (terrain, overlay, interactables).
- Building sprites placed via world-space nodes anchored to tile coordinates.
- Event-driven VFX overlays (selection, unlock, completion pulses).

### Asset Formats
- Textures/sprites: PNG (source), optional WebP for selected UI assets.
- Audio: WAV (master), OGG (runtime compressed).
- Config/data: JSON (human-readable) or TOML for balancing assets.
- Fonts: TTF/OTF with fallback stack.

### Atlas/Import Pipeline
- Use texture atlases for tile/building sprite groups.
- Standardize sprite pivots, tile sizes, and naming conventions.
- Import presets:
  - Pixel-style: nearest filtering, no mipmaps.
  - Smooth style: bilinear filtering + controlled compression.
- Pre-commit validation script checks:
  - missing references,
  - duplicate IDs,
  - atlas consistency,
  - tile size constraints.

### Compression and Performance Rules
- Prefer VRAM-compressed textures for large static atlases.
- Keep UI atlases separate from world atlases.
- Define LOD-equivalent strategy via culling/chunk visibility for large maps.

---

## UI Solution and Architecture Integration

### UI Framework
- Godot Control-based UI (HUD, side panels, objective tracker, modals).

### Integration Pattern
- UI consumes **read models only** (projection outputs from Application layer).
- UI actions emit **commands** (`ExpandTile`, `PlaceBuilding`, `UpgradeBuilding`).
- No direct domain-state mutation from UI nodes.
- Event-driven notification layer listens to domain events and shows toasts/banners.

### UI Composition
- HUD root (currency, rates, objective snippet).
- Context panel (selected tile/building details).
- Build panel (available actions filtered by unlock state).
- Notification layer (milestones/errors/rewards).

---

## Desktop Input Abstraction (Portability)

### Input Model
- Action map abstraction (engine input mapped to semantic actions).
- Mouse:
  - Left click: primary action/select/place.
  - Right click: cancel/back/context.
  - Wheel: zoom.
- Keyboard:
  - Mode switching (inspect/build/expand).
  - Camera pan shortcuts.
  - Debug toggles (dev builds only).

### Portability Rules
- Never bind gameplay directly to physical scan codes in Domain/Application.
- Keep input-to-command mapping in Presentation adapter.
- Provide configurable input bindings persisted in settings.

---

## Persistence Tooling (Aligned to Stage 2)

### Serialization Format
- Save files: JSON for debuggability in development, with optional compressed binary envelope for release.
- Schema includes explicit `schemaVersion`.

### Versioning and Migration Workflow
- Any breaking model change requires:
  1. version bump,
  2. migration class `vN_to_vNplus1`,
  3. migration unit tests,
  4. backward-compat load test fixtures.

### Save Structure
- `meta`, `world`, `economy`, `progression`, `settings` partitions (as Stage 2).
- Autosave interval + key-event save triggers (objective completion, major purchases).

### Offline Progress
- On load, compute elapsed time from `lastSaveUtc`.
- Clamp to maximum offline window.
- Apply deterministic aggregate economy simulation.
- Emit `OfflineRewardGranted` for UI summary.

---

## Build, Packaging, and Distribution Plan

### Local Build Targets
- Windows: x86_64 executable bundle.
- Linux: x86_64 build + AppImage packaging option.
- macOS: universal/x86_64+arm64 app bundle (as team resources allow).

### CI/CD Recommendation
Use GitHub Actions with matrix builds:
- `windows-latest`, `ubuntu-latest`, `macos-latest`.
- Steps:
  1. restore dependencies,
  2. run lint/format checks,
  3. run tests,
  4. export platform builds,
  5. attach artifacts.

### Release Workflow
- Tag-based releases (`v0.x.y`).
- Generate checksums for artifacts.
- Maintain release notes with known issues.
- For macOS, plan notarization/signing pipeline as a dedicated production task.

---

## Development Environment Setup

### Repository Mapping (from Stage 2)
- `/src/domain` → pure simulation and models.
- `/src/application` → command routing, projections, orchestration services.
- `/src/infrastructure` → persistence/content loader/logging.
- `/src/presentation` → Godot adapters (input/UI/feedback).
- `/data` → tile/building/objective/economy data assets.
- `/tests` → domain, application, integration suites.

### Dependency Management
- .NET solution with multiple projects:
  - `LandBuilder.Domain`
  - `LandBuilder.Application`
  - `LandBuilder.Infrastructure`
  - `LandBuilder.Presentation.Godot`
  - `LandBuilder.Tests.*`
- NuGet lock files enabled.

### Code Style and Static Analysis
- `.editorconfig` for naming/style conventions.
- Roslyn analyzers enabled (warnings as errors in CI where practical).
- Formatting gate: `dotnet format --verify-no-changes`.

### IDE/Editor Baseline
- Rider or VS Code + C# Dev Kit + Godot extension.
- Shared launch/debug profiles for game + tests.

---

## Testing and Debugging Strategy

### Test Layers
1. **Unit tests (Domain):**
   - cost curves,
   - placement validation,
   - objective predicates,
   - economy transactions.
2. **Application integration tests:**
   - command-to-event flows,
   - read-model projection correctness,
   - save/load orchestration.
3. **Determinism tests:**
   - same seed + command sequence => identical end state.
   - fixed-tick replay tests.
4. **Persistence compatibility tests:**
   - load fixtures from prior schema versions and migrate.

### Debug/Telemetry Tooling
- In-game debug panel:
  - tick rate/time scale,
  - currency injection,
  - unlock flags.
- Event trace log channel with filters.
- Economy graph overlay (PPM/SPM over time).
- Optional replay capture for bug reproduction.

---

## Risks and Mitigations (Stack-Specific)

1. **Determinism drift due to float math/time coupling**
   - Mitigation: fixed-step simulation, deterministic ordering, decimal/fixed-point for critical economy calculations.
2. **UI complexity leaking business logic**
   - Mitigation: strict read-model + command boundary; domain assemblies have no Godot references.
3. **Cross-platform export quirks**
   - Mitigation: CI matrix build on every release branch; smoke test checklist per OS.
4. **Asset pipeline inconsistency**
   - Mitigation: standardized import presets + validation scripts in CI.
5. **Save migration regressions**
   - Mitigation: mandatory migration tests and fixture-based backward compatibility suite.

---

## Decision

**Adopt Godot 4.3+ with C# (.NET 8) as the default production stack.**

Why this decision:
- Best balance of cross-platform desktop support, 2D productivity, architecture cleanliness, and educational transparency.
- Directly compatible with Stage 2 system boundaries and deterministic simulation requirements.
- Lower operational friction than Unity for this scope, and lower onboarding/tooling cost than Bevy for current goals.

---

## Stage 3 Exit Criteria (must be true before Stage 4)

Stage 3 is complete only when:
- Engine/framework decision is finalized and documented.
- Language/runtime version is fixed.
- Rendering + asset pipeline standards are defined.
- UI/input architecture contracts are documented against Stage 2 boundaries.
- Save format/versioning/migration workflow is specified.
- CI build/export plan for Windows/Linux/macOS is defined.
- Development tooling (format/lint/tests/debug) is selected and documented.
- Stack-specific risks and mitigations are agreed.

**Status:** Complete. Ready to proceed to Stage 4 (Minimum Viable Product Definition) in the next step.
