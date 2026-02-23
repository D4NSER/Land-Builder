# Stage 13: Deterministic Session Continuity & Replay Safety

## Scope
Stage 13 hardens deterministic session continuity for the tile-loop core by ensuring save/load contains complete deterministic state and replay after load stays bit-for-bit equivalent at the GameState level.

## What changed
- Added explicit GameState structural equality/hash support so tests can assert strict GameState equivalence.
- SaveRepository now exposes `TryLoad(path, out state, out error)` with explicit corruption/version rejection.
- SaveRepository `Load(path)` now throws `InvalidDataException` on invalid saves instead of silently mutating to defaults.
- Added replay continuity test: save mid-run, load, continue commands, compare to control run.
- Added corruption rejection test for malformed JSON.

## Deterministic state snapshot fields
- Coins
- RNG state (`RngState`, `RngStep`)
- Current tile in hand
- Board occupancy and tile rotations
- Last message

## Validation commands
```powershell
dotnet clean .\LandBuilder.sln
dotnet build .\LandBuilder.sln
dotnet test .\LandBuilder.sln
```
