# Presentation Design Deconstruction (Original) + Implementation Plan

## Purpose
This document translates the visual and UX strengths of the mobile hex-builder genre into an original implementation plan for this project.

It is intentionally **not** a 1:1 recreation guide for any commercial game UI/art. The goal is:
- preserve the deterministic core (`LandBuilder.Core`)
- improve presentation quality
- match genre-level readability and polish
- keep all assets procedural / original

## Guardrails
- Do not copy proprietary UI layouts, icons, textures, or assets.
- Do not modify `LandBuilder.Core` for visual-only goals.
- Keep deterministic gameplay and saves intact.
- Procedural visual randomness must be deterministic from `(slotIndex + fixed salt)`.

## Current Presentation Baseline (Observed)
- Strong foundation already exists:
  - 3D hex-like board presentation
  - procedural terrain/dioramas
  - hover, preview, placement animation
  - camera orbit/zoom
  - save/load + score/high-score integration
  - neighbor-aware seams / river blending
  - water layering + ripple illusion
  - outdoor lighting/fog polish
- Remaining gaps are mostly about:
  - UX clarity at fullscreen scale
  - stronger visual hierarchy
  - more consistent art direction
  - replacing “debug-style controls” with an in-world / minimal HUD loop
  - tile grammar closer to generative “terrain composition”

## Genre Design Deconstruction (Principles, Not Copies)

### 1) Why these games feel good
- Big readable playfield with minimal HUD clutter.
- Clear action priority:
  - draw tile
  - place tile
  - repeat
- Strong hover/preview feedback.
- Soft lighting and pastel-ish environmental separation.
- Terrain reads at a glance via shape + color + silhouette.
- Motion is subtle but constant:
  - ambient light drift
  - tiny bobbing
  - water movement
  - soft tweening on interactions

### 2) What makes the board readable
- High contrast between:
  - playable terrain
  - empty cells
  - water/background
- Controlled saturation:
  - focal objects slightly richer
  - background and far decor slightly muted
- Consistent directional lighting for shape legibility.
- Edge blending / seam hiding between adjacent tiles.

### 3) What makes HUD feel “mobile-clean”
- Few persistent controls.
- Large touch-friendly primary action.
- Secondary systems hidden/collapsible.
- Metrics grouped in chips/pills (coins/score/high score).
- Context-sensitive hints instead of static instructions.

## Desired Product Direction (This Project)

### Design Goal
An original “tabletop outdoor diorama” interface where:
- the board is the focus
- core loop is draw/place/connect
- HUD is reduced to essential info + one or two actions
- tile visuals feel generative and varied
- interactions are keyboard/mouse friendly on desktop

### Functional Goal (Presentation + Light Game Loop Framing)
Without changing `LandBuilder.Core`, present a mode where:
- unlock controls are hidden from primary UI
- draw/place loop is primary
- random tiles are drawn and placed with adjacency validity enforced by the core
- the starting tile is pre-placed for better onboarding feel (presentation bootstrapping via initial scripted setup or first-load template state)

Note: if “starting tile already there” requires game state initialization changes, implement it in the presentation layer by issuing deterministic startup commands after session creation (not by changing core rules).

## Key Requested Changes Mapped to Architecture

### A) Fullscreen-first experience
Presentation-only changes:
- Make HUD responsive to viewport size.
- Anchor board center and let camera framing adapt.
- Scale/position HUD panels for widescreen and 16:9 / 16:10.
- Add “minimal mode” layout for primary play.

Core impact: none.

### B) “No buttons / no unlocks” feel (Primary UX)
Interpretation for implementation:
- Hide unlock controls and non-essential debug controls from main view.
- Keep save/load/high-score available in a secondary panel or hotkeys.
- Keep core unlock system intact but disable/auto-configure in presentation mode.

Two safe options:
1. **Sandbox mode UI** (recommended first):
   - hide unlock panel
   - pre-unlock all tiles in presentation startup sequence using deterministic commands
2. **Classic progression mode UI**:
   - existing unlock panel remains available in a menu toggle

Core impact: none.

### C) Generative tile composition (“1/6 plains, 5/6 water”, etc.)
This is a visual-system feature, not a core-rule feature.

Approach:
- Keep `TileType` as gameplay identity.
- Add procedural sub-biome masks inside a tile for visuals:
  - e.g., a `River` tile can render mostly land with a broad riverbed, or mostly water with small land banks.
  - `Plains/Meadow/Woods` can blend patches and edge clusters.
- Use deterministic per-slot variation:
  - proportions derived from `(slotIndex + salt)` and neighbor context.
- Do not alter placement validation/scoring semantics in core.

Core impact: none.

## Implementation Plan (Detailed, Ordered)

## Phase 0 — Documentation + Design Freeze (This Step)
Deliverables:
- This document
- Agreement on target mode:
  - `Minimal Play Mode` (recommended)
  - `Progression Mode` (existing systems exposed)

Decisions to confirm before coding:
- Should unlock progression remain accessible at all in the main scene?
- Should the “first tile placed” happen automatically every new game?
- Should save/load remain visible or move to a small menu/hotkeys?

## Phase 1 — Fullscreen / Responsive Presentation Shell
Files:
- `scenes/Main.tscn`
- `src/Presentation/MainController.cs`

Tasks:
1. Convert HUD layout to fullscreen-first responsive anchors.
2. Keep top chip row compact and always visible.
3. Collapse secondary controls into a hidden/expandable utility panel.
4. Increase board camera framing at startup based on viewport aspect ratio.
5. Ensure no important UI overlaps the board center on common resolutions.

Acceptance criteria:
- At 1080p and 1440p, board remains primary visual focus.
- UI remains readable and uncluttered.

## Phase 2 — Minimal Core Loop UX (Draw / Place / Connect)
Files:
- `scenes/Main.tscn`
- `src/Presentation/MainController.cs`

Tasks:
1. Hide unlock buttons from primary HUD.
2. Keep `Draw` as primary CTA.
3. Add hotkeys:
   - `Space` => Draw
   - `Enter` => Submit score (optional)
   - `Tab` => toggle utility panel
4. Make placement preview more prominent than UI controls.
5. Add stronger invalid-placement feedback (camera shake/light pulse/brief message tint) in presentation only.

Acceptance criteria:
- A user can play the main loop with almost no button hunting.
- Core command flow remains unchanged.

## Phase 3 — Start-State Onboarding (First Tile Already There)
Files:
- `src/Presentation/MainController.cs`

Tasks:
1. On new game, deterministically bootstrap the board using presentation-issued commands:
   - draw tile
   - place on a predefined slot (e.g., center)
2. If placement fails (rare due to state assumptions), fall back to manual start.
3. Mark this as “presentation onboarding mode” so load flows are unaffected.

Acceptance criteria:
- New sessions begin with a visible starter tile on the board.
- `LandBuilder.Core` remains unmodified.

## Phase 4 — Generative Terrain Composition Layer
Files:
- `src/Presentation/MainController.cs`

Tasks:
1. Add per-slot terrain composition parameters derived from `(slotIndex + salt)`:
   - land/water ratio
   - patch count
   - patch spread
   - roughness tint offsets
2. Expand tile builders to render mixed sub-regions:
   - River/Lake: varying water dominance
   - Plains/Meadow: blended dirt/grass/flower zones
   - Woods: density gradients near edges
3. Ensure neighbor-aware blending respects composition:
   - connected water edges look continuous
   - seam skirt still hides gaps
4. Cap detail mesh counts for performance.

Acceptance criteria:
- Same `TileType` can look noticeably varied across slots.
- Visual variation remains deterministic and stable across save/load/replay.

## Phase 5 — Professional HUD Visual Pass (Original Design)
Files:
- `scenes/Main.tscn`
- `src/Presentation/MainController.cs`

Tasks:
1. Refine chip/panel spacing, padding, typography hierarchy.
2. Reduce debug-text density:
   - move event log to collapsible/mini drawer
   - shorten status lines
3. Add a compact current-tile card:
   - tile name
   - rotation
   - optional mini visual swatch
4. Add polished “focus” / “draw” emphasis states:
   - hover
   - pressed
   - disabled
5. Add fullscreen-safe margins and safe-zone padding.

Acceptance criteria:
- Interface feels intentional and production-like, not a dev tool.
- No direct visual borrowing from commercial UI layouts.

## Phase 6 — Camera / Composition Polish
Files:
- `src/Presentation/MainController.cs`

Tasks:
1. Aspect-aware default camera framing.
2. Softer orbit damping (already started).
3. Optional edge pan or “focus tile” camera easing.
4. Add presentation camera presets:
   - gameplay view
   - beauty view (optional)

Acceptance criteria:
- Board reads clearly at a glance in fullscreen.
- Camera motion feels smooth and unobtrusive.

## Phase 7 — Readability + Accessibility Pass
Files:
- `scenes/Main.tscn`
- `src/Presentation/MainController.cs`

Tasks:
1. Ensure label contrast on bright sky backgrounds.
2. Add colorblind-safe differentiation support (optional mode):
   - silhouette/shape cues stronger than color alone
3. Scale text for high DPI displays.
4. Add “reduced motion” toggle (presentation-only).

Acceptance criteria:
- Readability remains good on multiple displays.

## Phase 8 — Performance Guardrails
Files:
- `src/Presentation/MainController.cs`

Tasks:
1. Track rough MeshInstance count and log warning if exceeded.
2. Avoid rebuilding tile visuals unless signature changes (already in progress).
3. Reuse shared materials where possible for static props.
4. Keep ripple/idle animations allocation-free per frame.

Acceptance criteria:
- Stable frame pacing for current 3x3 board in editor.

## Phase 9 — QA / Validation Checklist
Commands:
```powershell
dotnet build LandBuilder.Presentation.csproj
dotnet test LandBuilder.sln
```

Manual Godot checks:
1. Fullscreen / maximized window layout readability.
2. Draw/place loop works without opening utility controls.
3. Starter tile is present on new game (if enabled).
4. Save/load preserves visuals (tile reconstruction deterministic).
5. Water and terrain variation remains stable across reload.
6. Hover/preview/camera remain responsive.

## Current Completion Status (Implemented)
- Phase 1: Completed
  - responsive HUD shell
  - compact top chip row
  - collapsible utility panel
  - fullscreen-safe panel layout
- Phase 2: Completed
  - minimal play mode defaults
  - `Space` draw / `Enter` submit / `Tab` utility toggle
  - stronger invalid placement feedback (status flash)
- Phase 3: Completed
  - deterministic starter tile bootstrap on new session (presentation-issued commands)
- Phase 4: Completed (first production-ready pass)
  - deterministic per-slot composition parameters
  - mixed River/Lake land/water variants
  - clustered biome zoning for Plains/Meadow/Woods
  - neighbor-aware seam/river blending preserved
- Phase 5: Completed (first production-ready pass)
  - current-tile card + swatch
  - reduced debug text density
  - action rail grouping and button states
  - fullscreen-safe HUD spacing
- Phase 6: Completed (first production-ready pass)
  - aspect-aware default camera framing
  - hover/placement focus easing
- Phase 7: Deferred / optional follow-up
  - accessibility-specific toggles (reduced motion, colorblind mode, DPI mode) not yet implemented
- Phase 8: Completed
  - lightweight mesh/node count sampling
  - hidden utility-only metrics display
  - mesh count warning event when over threshold
- Phase 9: Partially completed
  - automated build/test validation completed
  - manual Godot smoke checklist remains to be run in-editor

## Recommended Next Implementation Order (Practical)
If starting immediately, do this sequence:
1. `Phase 1` fullscreen responsive HUD cleanup
2. `Phase 2` minimal loop UX (hide unlocks/buttons into utility panel)
3. `Phase 3` starter tile onboarding
4. `Phase 5` professional HUD visual pass
5. `Phase 4` generative sub-tile terrain composition
6. `Phase 6` camera polish
7. `Phase 8` performance pass

Reason:
- Improves feel quickly without destabilizing rendering code too early.
- Keeps core gameplay playable while visuals evolve.

## Explicit Non-Goals (to avoid scope creep)
- Reproducing a commercial game’s exact UI layout
- Copying icons, textures, assets, or effects 1:1
- Changing deterministic core rules in `LandBuilder.Core`
- Network, monetization, or live-ops style UI systems

## Implementation Notes for This Repo
- Presentation-only behavior should live in `src/Presentation/MainController.cs`.
- Scene structure changes should stay in `scenes/Main.tscn`.
- Keep `LandBuilder.Core` commands as the source of truth.
- If a feature is “visual only,” derive it from:
  - `slotIndex`
  - `TileType`
  - current neighbors
  - fixed salts
  - current time (for animation only)

## Ready-to-Start Task List (Actionable)
- [x] Add fullscreen-responsive HUD layout pass
- [x] Add minimal mode (hide unlocks + utility panel toggle)
- [x] Add startup pre-placed tile flow (presentation-issued commands)
- [x] Add current-tile card polish
- [x] Add generative sub-biome composition parameters per slot
- [x] Add mixed land/water proportions for River/Lake visuals
- [x] Add stronger invalid placement visual feedback
- [x] Add performance counters / debug overlay (optional hidden)
- [ ] Run build + tests and manual Godot smoke pass
