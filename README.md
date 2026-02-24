# Land-Builder

Structured project documentation is being developed in stages.

- Stage 1: Game Analysis and Scope Definition → `docs/stage1_game_analysis_scope.md`
- Stage 2: System Architecture Design → `docs/stage2_system_architecture_design.md`
- Stage 3: Technology Stack and Tooling → `docs/stage3_technology_stack_and_tooling.md`
- Stage 4: Minimum Viable Product Definition → `docs/stage4_mvp_definition.md`
- Stage 5: Implementation Roadmap → `docs/stage5_implementation_roadmap.md`
- Stage 6 (MVP-0): Step-by-Step Build Guidance → `docs/stage6_mvp0_vertical_slice.md`
- Stage 6 (MVP-1): Core Playable Loop Build → `docs/stage6_mvp1_build.md`
- Stage 6 (MVP-2): Progression + Save/Load Reliability Build → `docs/stage6_mvp2_build.md`
- Stage 6 (MVP-3): Usability + Stabilization Build → `docs/stage6_mvp3_build.md`
- Stage 7: Production Polish & Hardening → `docs/stage7_production_polish_plan.md`
- Stage 8A: Placement UX + Expansion Cost Scaling → `docs/stage8a_placement_and_expansion.md`
- Stage 8B: Minimal Multi-Building Production Chain → `docs/stage8b_minimal_production_chain.md`
- Stage 8C: Chapter 2 Objective Chain Extension → `docs/stage8c_chapter2_objectives.md`
- Stage 9: Windows Dev Setup → `docs/dev_setup_windows.md`
- Stage 9: Desktop Export Checklist → `docs/export_checklist.md`
- Stage 10A: Map Loop Expansion (Deterministic Multi-Pocket Topology) → `docs/stage10a_map_loop_expansion.md`
- Stage 10B: Small Building Set Parity (Exactly +2 Buildings) → `docs/stage10b_small_building_set_parity.md`
- Stage 10C: Economy Pacing + Objective Continuity (Deterministic Tuning) → `docs/stage10c_economy_pacing_and_continuity.md`
- Stage 11: Godot Desktop Integration & Smoke Exports → `docs/stage11_godot_desktop_integration_and_smoke_exports.md`
- Stage 12: Godot Integration Fixes → `docs/stage12_godot_integration_fixes.md`
- Land Builder Game Spec (Reset) → `docs/land_builder_game_spec.md`
- Stage 13: Deterministic Session Continuity & Replay Safety → `docs/stage13_deterministic_session_continuity.md`
- Stage 14: Unlockable Tile Progression System → `docs\stage14_unlockable_tile_progression.md`
- Stage 15: Deterministic Scoring + High Score Persistence → `docs\stage15_deterministic_scoring_and_highscore.md`
## Godot 4 Prototype (Current Presentation)

Open the project in Godot 4.x with .NET support and run the main scene (`res://scenes/Main.tscn`).

### Core Controls
- `LMB`: place current tile on hovered slot
- `Q` / `E`: rotate tile preview/placement
- `Space`: draw tile
- `Enter`: submit score
- `RMB Drag`: orbit camera
- `Mouse Wheel`: zoom
- `F`: focus/center camera
- `T`: toggle empty-slot plus overlays
- `Tab`: toggle utility panel (menu)

### Minimal Mode UX
- The scene defaults to a board-first minimal mode.
- A starter tile is placed automatically on new sessions (presentation-only onboarding).
- Utility controls (save/load/new game/unlocks/tools) are hidden until `Tab` or `Menu`.

### Utility Panel / Metrics
- When the utility panel is open, the `Recent` event panel title shows lightweight perf metrics:
  - mesh instance count
  - node count
- A soft UI warning is emitted if mesh instances exceed the current presentation threshold (`150`).

### Save / High Score Paths
- Save path defaults to `user://save.json` (editable in utility panel)
- High score path defaults to `user://highscore.json`
