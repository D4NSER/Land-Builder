# Stage 6 – MVP-3 Build (Usability + Stabilization)

## 1) Scope Lock (Implemented)

### In scope
- Input usability improvements: hotkeys, camera pan/zoom, cancel/back flow.
- HUD/notification readability updates.
- Determinism stress testing.
- Save/load stability testing.
- Bug fixes found by those tests/usability checks only.

### Out of scope
- New gameplay systems, new economy/progression mechanics, content expansion.
- Stage 7 polish work (advanced VFX/audio/localization/etc.).

---

## 2) File Map

### Presentation
- `src/Presentation/MainController.cs`
- `scenes/main.tscn`

### Tests
- `tests/LandBuilder.Tests/Mvp3DeterminismStressTests.cs` (new)
- `tests/LandBuilder.Tests/Mvp3SaveLoadStabilityTests.cs` (new)

### Documentation
- `docs/stage6_mvp3_build.md` (new)
- `README.md` (Stage 6 MVP-3 link)

---

## 3) Implementation Notes

1. Added keyboard shortcuts for key actions:
   - `1`/`2` expand tiles
   - `C` place camp, `Q` place quarry, `U` upgrade building
   - `Ctrl+S` save, `Ctrl+L` load
   - `Esc` cancel/back feedback
2. Added camera usability controls:
   - Arrow keys / WASD pan
   - `+` / `-` zoom in/out
3. Improved readability:
   - in-scene hotkeys label
   - rolling notification list (last 5 entries)
4. Added autosave cooldown (750 ms) to avoid notification/autosave spam during rapid event bursts.

---

## 4) Measurable Verification

### Automated
- Build: `dotnet build .\LandBuilder.sln`
- Test list: `dotnet test .\LandBuilder.sln --list-tests`
- Run tests: `dotnet test .\LandBuilder.sln`

### New stability scenarios
1. Determinism stress:
   - Test: `Mvp3DeterminismStressTests.DeterminismStress_10000Ticks_FixedStream_Repeated5Runs_HasIdenticalStateHash`
   - Scenario: `N = 10,000` ticks, fixed command stream, repeated `K = 5` runs.
   - Requirement: final state hash identical for all runs.
2. Save/load stability:
   - Test: `Mvp3SaveLoadStabilityTests.SaveLoadStability_50RoundTrips_NoStateDrift`
   - Scenario: `50` repeated save/load round-trips with deterministic tick progression between saves.
   - Requirement: state hash before save equals state hash after load for every cycle.

### Manual QA (Godot)
- Run `scenes/main.tscn`.
- Validate hotkeys, camera pan/zoom, cancel behavior.
- Confirm objective and notification readability.
- Confirm autosave triggers on key events but does not spam excessively.

---

## 5) MVP-3 Done Criteria

MVP-3 is done when:
1. Input/hotkey/camera/cancel usability improvements are present and working.
2. HUD/notification readability is improved without adding new systems.
3. Determinism stress test passes with fixed scenario and repeated runs.
4. Save/load stability test passes across repeated round-trips.
5. No gameplay rules/features were added beyond MVP-2; this remains stabilization-only.

**Stop condition:** MVP-3 complete. No Stage 7 work in this change.
