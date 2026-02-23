# Windows Development Setup (Stage 9)

## Recommended versions
- **Godot:** `4.2.2-stable` (Mono/.NET build)
- **.NET SDK:** `8.0.x` (recommend latest `8.0 LTS` patch, e.g. `8.0.4xx`)

> Use Godot 4.x .NET/Mono editor build so C# project generation and script compilation are available.

## 1) Verify .NET is installed
From PowerShell or Command Prompt:

```powershell
dotnet --version
```

Expected: a version starting with `8.0`.

If command is not found:
- Install .NET 8 SDK from Microsoft.
- Restart terminal and verify again.

## 2) Open/import the project in Godot
1. Launch **Godot 4.2.2 .NET** editor.
2. Click **Import**.
3. Select `project.godot` from repo root.
4. Confirm import and open project.

## 3) Build C# scripts in Godot
After project opens:
1. Go to **Project > Tools > C# > Create C# Solution** (only if prompted on first run).
2. Then run **Build > Build Project** (or use the C# build button in the Script pane).
3. Wait for successful compilation.

Common first-run behavior:
- First build can take longer due to restore/compile.
- Godot may generate `.godot/mono` metadata and solution artifacts.

## 4) Run the game scene
1. In FileSystem dock, open `scenes/main.tscn`.
2. Run scene (Play Scene) or run project (Play Project).

Expected behavior (sanity):
- UI loads with objective/progression labels.
- Buttons/hotkeys for expand/place/upgrade/save/load respond.
- Deterministic progression updates after commands/ticks.

## 5) CLI build/test workflow (Core + Tests only)
From repo root in PowerShell:

```powershell
dotnet build .\LandBuilder.sln
dotnet test .\LandBuilder.sln
```

Expected: build succeeds and tests pass.

Why this works without Godot installed:
- `LandBuilder.sln` intentionally includes only `LandBuilder.Core` and `LandBuilder.Tests`.
- Godot-dependent presentation scripts (`src/Presentation/MainController.cs`) are compiled by the Godot .NET editor pipeline, not by the CLI solution build.

## 6) Save file location on Windows (`user://` mapping)
Godot `user://` saves are typically under:

```text
%APPDATA%\Godot\app_userdata\Land-Builder\
```

For this project, save names include:
- `mvp2_save.json`
- `mvp2_save.backup.json`
- `mvp2_autosave.json`
- `mvp2_autosave.backup.json`

## Troubleshooting

### SmartScreen blocks executable
- Click **More info** > **Run anyway** for local test builds, or code-sign for distribution builds.

### `dotnet` missing or wrong version
- Ensure .NET 8 SDK installed (not runtime-only).
- Verify `dotnet --version` returns `8.0.*`.

### Godot C# build errors on first load
- Confirm you installed **Godot .NET/Mono build**, not standard non-.NET editor.
- Regenerate C# solution via **Project > Tools > C#**.
- Rebuild project in editor.

### Missing assemblies / compile failures
- Close Godot.
- Run from repo root:
  - `dotnet restore .\LandBuilder.sln`
  - `dotnet build .\LandBuilder.sln`
- Reopen Godot and rebuild.

### Tests pass in CLI but Godot fails to run scripts
- Ensure Godot editor is pointing to installed .NET SDK.
- Delete transient `.godot/mono` folder and reopen project to regenerate if needed.


## Stage 11 addendum
- Use **Godot 4.2.2-stable .NET** + **.NET SDK 8.0.x** only.
- For full Windows C# integration workflow, failure matrix, deterministic smoke script, and required evidence bundle, see: `docs/stage11_godot_desktop_integration_and_smoke_exports.md`.
- Quick run order:
  1. `dotnet --version` (must be `8.0.x`)
  2. Open `project.godot` in Godot .NET build
  3. `Project -> Tools -> C# -> Create C# Solution` (if prompted)
  4. `Build -> Build Project`
  5. Run `scenes/main.tscn` and execute deterministic mini-script from Stage 11 doc.
