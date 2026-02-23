# Stage 12: Godot Integration Fixes

## Root cause
Godot was configured to compile/load a presentation assembly named `LandBuilder.Presentation`, but the repository did not contain a dedicated presentation C# project file in source control. This can cause inconsistent C# script build/load behavior across machines when Godot-generated project artifacts are absent or stale.

## What changed
- Added root `LandBuilder.Presentation.csproj` (Godot SDK project).
  - Targets `net8.0`
  - `AssemblyName` set to `LandBuilder.Presentation`
  - `RootNamespace` set to `LandBuilder.Presentation`
  - References `src/LandBuilder.Core/LandBuilder.Core.csproj` and compiles scripts under `src/Presentation/**/*.cs`
- Kept `LandBuilder.Presentation.csproj` for Godot C# integration, but excluded it from `LandBuilder.sln` so CLI build/test do not require Godot SDK references.
- Verified `scenes/main.tscn` script binding still points to `res://src/Presentation/MainController.cs` and class namespace remains `LandBuilder.Presentation.MainController`.

## Windows Godot editor verification steps
1. Install .NET SDK 8.0.x and verify:
   - `dotnet --version`
2. Open **Godot 4.2.2-stable .NET**.
3. Import `project.godot`.
4. In Godot, if prompted: `Project -> Tools -> C# -> Create C# Solution`.
5. Build scripts: `Build -> Build Project`.
6. Run `scenes/main.tscn`.
7. Confirm there are no script load / namespace / assembly errors.

## Export smoke (Windows)
Use the Stage 11 checklist and deterministic mini-script from:
- `docs/stage11_godot_desktop_integration_and_smoke_exports.md`

Focus checks:
- C# scripts are built before export.
- Exported `.exe` launches.
- No assembly/script load failures in exported run.


## Workflow split (intended)
- **CLI (`dotnet build/test LandBuilder.sln`)** builds only `LandBuilder.Core` + `LandBuilder.Tests`.
- **Godot editor** builds `LandBuilder.Presentation.csproj` (Godot SDK) which includes scripts from `src/Presentation/`.
- This keeps automated test/build reproducible on machines without Godot while preserving playable editor/runtime behavior.
