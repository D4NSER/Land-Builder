# Desktop Export Checklist (Stage 9)

## Scope
Desktop export readiness for:
- Windows
- Linux
- macOS

No gameplay feature changes are included in this checklist.

## Prerequisites

### Common
- Godot **4.2.2-stable .NET/Mono** installed.
- .NET SDK **8.0.x** installed.
- Project builds/tests pass from repo root:
  - `dotnet build .\LandBuilder.sln`
  - `dotnet test .\LandBuilder.sln`

### Windows export host
- Windows 10/11
- Export templates installed in Godot

### Linux export host
- Linux distro with required runtime libs for Godot exports
- Export templates installed in Godot

### macOS export host
- macOS host required for notarization/signing workflows (if distributing broadly)
- Export templates installed in Godot

## Export preset setup (Godot 4.x)
1. Open project in Godot .NET editor.
2. Go to **Project > Export...**.
3. Add preset: **Windows Desktop**.
4. Add preset: **Linux/X11** (or Linux desktop preset for your Godot version).
5. Add preset: **macOS**.
6. For each preset, set export path under `builds/<os>/`.
7. Save presets to `export_presets.cfg` in repo root.

## Output naming & folder structure
Recommended structure:

```text
/builds
  /windows
    LandBuilder-Windows-x64.exe
  /linux
    LandBuilder-Linux-x64.x86_64
  /macos
    LandBuilder-macOS.app
```

Naming convention:
- `LandBuilder-<OS>-<Arch>[.<ext>]`

## Smoke test matrix

| OS | Launch | Input sanity | Expand/Place/Upgrade/Cancel | Save/Load | Objective updates | Determinism spot-check |
|---|---|---|---|---|---|---|
| Windows | App opens to main scene | Buttons + hotkeys respond | Run one full interaction cycle | Save, restart app, load | Objective/progress labels update correctly | Repeat same 10-command script; verify same resulting coins/buildings |
| Linux | App opens to main scene | Buttons + hotkeys respond | Run one full interaction cycle | Save, restart app, load | Objective/progress labels update correctly | Repeat same 10-command script; verify same resulting coins/buildings |
| macOS | App opens to main scene | Buttons + hotkeys respond | Run one full interaction cycle | Save, restart app, load | Objective/progress labels update correctly | Repeat same 10-command script; verify same resulting coins/buildings |

## Artifacts to capture during export testing
For each OS test run, capture:
1. **Screenshot** of main scene after launch.
2. **Screenshot** after expand/place/upgrade actions.
3. **Screenshot or note** showing save then load success.
4. **Log snippet** for app launch and any warnings/errors.
5. **Determinism note** with command sequence and resulting key values (coins, building count, objective index).

## Exit criteria
- All 3 desktop presets exist and export without critical errors.
- Smoke matrix rows pass for Windows/macOS/Linux.
- Build/test gate remains green before and after export validation.


## Stage 11 Windows verified-export addendum
1. Confirm Godot export templates are installed for the pinned version (`4.2.2-stable .NET`).
2. Before export, run C# build in-editor (`Build -> Build Project`).
3. Export using Windows preset to `builds/windows/`.
4. Run exported `.exe` and execute the deterministic mini-script documented in `docs/stage11_godot_desktop_integration_and_smoke_exports.md`.
5. Validate and capture artifacts: startup screenshot, post-action screenshot, save/load proof, run log snippet, and final key values (`Coins`, `CurrentObjectiveIndex`, building counts).
