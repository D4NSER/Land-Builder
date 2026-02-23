# Stage 11: Godot Desktop Integration & Smoke Exports

## Scope
Stage 11 is **documentation-only** hardening for reliable Windows Godot + C# execution and one verified Windows export workflow.

This stage does **not** change gameplay systems, deterministic simulation logic, economy, objectives, buildings, tests, or CI.

## Pinned tool versions (and why)
- **Godot editor:** `4.2.2-stable .NET`
- **.NET SDK:** `8.0.x` (LTS patch line)

Rationale:
- Matches existing project setup/export docs and keeps environment drift low.
- Godot `.NET` build is required for C# script compilation and assembly loading.
- `.NET 8` aligns with solution/test target runtime.

---

## Clean Windows runbook

### 1) Install and verify .NET SDK
In PowerShell:

```powershell
dotnet --version
```

Expected: `8.0.x`.

### 2) Download the correct Godot editor
- Download **Godot 4.2.2-stable .NET** (not the standard non-.NET editor).
- Confirm the executable name/build label includes `.NET`.

### 3) Import project
1. Open Godot 4.2.2 .NET editor.
2. Click **Import**.
3. Select repo `project.godot`.
4. Open the imported project.

### 4) Generate/restore/build C# scripts
1. If prompted: **Project -> Tools -> C# -> Create C# Solution**.
2. Build scripts: **Build -> Build Project**.
3. Wait for build to finish with no script compile errors.

### 5) Run the main scene
1. Open `scenes/main.tscn`.
2. Press **Play Scene** (or **Play Project**).

### 6) “It worked” indicators
- Main UI appears and responds to inputs.
- Expand/place/upgrade/save/load actions execute.
- Objective/progression text updates after deterministic actions.
- No C# assembly load/namespace errors in output panel.

---

## Failure-mode matrix

| Symptom | Likely cause | Fix | Verify |
|---|---|---|---|
| `dotnet` command missing or wrong major version | .NET SDK missing, or PATH points to old SDK/runtime-only install | Install .NET SDK 8.0.x, restart terminal/IDE | `dotnet --version` returns `8.0.x` |
| Godot opens project but C# options unavailable | Non-.NET Godot editor build installed | Install/use **Godot 4.2.2-stable .NET** build | `Project -> Tools -> C#` menu appears |
| `.mono`/generated C# metadata missing or corrupted; build repeatedly fails | Interrupted first-run generation or stale generated files | Close Godot, remove transient generated C# metadata (`.godot/mono`), reopen and rebuild | Build completes and scripts load |
| Assembly load or namespace/script mismatch errors at run | Stale build artifacts, script class/namespace mismatch, moved script path not reflected | Rebuild solution (`dotnet build .\\LandBuilder.sln`), rebuild in Godot, verify script paths + class names | Scene runs without assembly/script load errors |
| Export runs but C# project fails to start correctly | Export templates missing, scripts not built before export, runtime/dependency mismatch | Install export templates, build scripts first, re-export from proper preset | Exported `.exe` launches and executes smoke checklist |

---

## Windows smoke checklist
Run both A and B with the same deterministic mini-script.

### Deterministic mini-script
Use this exact sequence:
1. Expand first unlockable tile.
2. Enter Camp mode and place one Camp on a valid tile.
3. Tick simulation (`TickCommand`) enough to gain coins (same count each run).
4. Upgrade Building #1 once.
5. Save.
6. Load.

### Record these key values at end
- `Coins`
- `CurrentObjectiveIndex`
- Building counts by type (`Camp`, `Quarry`, `Sawmill`, `Forester`, `ClayWorks`)

### A) Editor run smoke
- [ ] Project opens in Godot 4.2.2 .NET.
- [ ] C# build succeeds.
- [ ] `scenes/main.tscn` runs.
- [ ] Mini-script succeeds end-to-end.
- [ ] Recorded key values captured.

### B) Exported Windows build smoke
- [ ] Windows export preset used (`export_presets.cfg`).
- [ ] Scripts built before export.
- [ ] Export produced `.exe` in `builds/windows/`.
- [ ] Exported app launches and runs mini-script.
- [ ] Recorded key values captured and compared to editor run.
- [ ] Values match expected deterministic outcome for same sequence.

---

## Required evidence bundle
For each Windows verification pass (Editor + Exported build), collect:
1. Screenshot: app launch/main scene.
2. Screenshot: post-actions state (after expand/place/upgrade).
3. Screenshot or log line: save then load success.
4. Output/log snippet: C# build + run status (warnings/errors if any).
5. Recorded deterministic sequence used.
6. Final recorded key values (`Coins`, `CurrentObjectiveIndex`, building counts by type).

This evidence set is the gate for “desktop integration verified.”
