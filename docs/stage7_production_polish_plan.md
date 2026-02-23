# Stage 7 – Production Polish & Hardening (Execution)

## Scope Guardrails

### In-scope
- UX polish (alignment, spacing, readability consistency)
- Input refinement (centralized keybindings, clarity, responsiveness)
- Save safety hardening (atomic save + backup + fallback load)
- Error handling improvements (clear fail-fast messages)
- Minor performance-safe stabilization fixes only
- Desktop export readiness checklist (Windows/macOS/Linux)

### Out-of-scope
- New gameplay systems
- New progression layers
- New building types
- Content expansion beyond MVP chain

---

## Exact Changes Implemented

1. **Input/keybinding centralization** in `MainController`:
   - Added `HotkeyBinding` map as single source of truth.
   - Added duplicate-conflict detection for keybindings (fail-fast on startup).
   - Help text now generated from binding map to stay consistent.
2. **UX/readability polish**:
   - Added rolling notifications label usage consistency.
   - Added explicit in-scene hotkeys line sourced from actual bindings.
3. **Save safety hardening** in `SaveRepository`:
   - Added atomic write strategy (`.tmp` then move).
   - Added optional backup write on every save.
   - Added `LoadWithRecovery(primary, backup, safeDefaultFactory)` with fallback messaging.
4. **Error handling hardening**:
   - Objective loader missing-file and validation errors now include actionable hints.
   - Save/load failures wrapped with clearer guidance while preserving inner exceptions.
5. **Stability tests extended**:
   - Added corruption fallback tests and safe-default fallback tests.
   - Added loader actionable error-message test.

---

## Files Changed

- `src/Presentation/MainController.cs`
- `scenes/main.tscn`
- `src/LandBuilder.Core/Infrastructure/SaveRepository.cs`
- `src/LandBuilder.Core/Infrastructure/Content/ObjectiveDefinitionLoader.cs`
- `tests/LandBuilder.Tests/Mvp3SaveLoadStabilityTests.cs`
- `docs/stage7_production_polish_plan.md`
- `README.md`

---

## Verification Commands

### Build/Test
- `dotnet build .\LandBuilder.sln`
- `dotnet test .\LandBuilder.sln --list-tests`
- `dotnet test .\LandBuilder.sln`

### Manual (Godot)
1. Open project in Godot 4.x.
2. Run `scenes/main.tscn`.
3. Verify:
   - Hotkeys trigger documented actions and match help text.
   - No conflicting shortcuts.
   - Camera pan/zoom and cancel flow responsive.
   - Save/load fallback message appears when primary is corrupted.
   - Notification feed stays readable and bounded.

---

## Desktop Export Readiness Checklist (Concise)

- **Windows**
  - .NET runtime present or self-contained export packaging strategy chosen.
  - Save path permissions validated in `%AppData%` user space (`user://`).
- **Linux**
  - Verify write permissions in user home for `user://` path.
  - Test keyboard layout behavior for hotkeys.
- **macOS**
  - Confirm app sandbox/write location access for `user://` path.
  - Verify key modifiers (`Cmd` vs `Ctrl`) are documented if remapping is needed.

(Export automation intentionally not added in this Stage 7 scope.)

---

## Done Criteria

1. Input bindings are centralized, conflict-checked, and help text-consistent.
2. UI readability improves without adding gameplay functionality.
3. Save pipeline uses atomic writes and backup path.
4. Load path recovers primary→backup→safe-default with clear messages.
5. Stability tests include corrupted-save fallback and pass.
6. No new gameplay/progression/content systems were introduced.

