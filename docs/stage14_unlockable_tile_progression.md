# Stage 14: Unlockable Tile Progression System

## Overview
Stage 14 adds deterministic tile unlock progression to the tile-loop core. Tiles are no longer all available by default. Players must spend coins to unlock additional tile types, and the unlocked set becomes part of the deterministic game state snapshot.

## Unlock Rules
- `TileType.Plains` starts unlocked by default and has unlock cost `0`.
- Other tile types require coins and must be unlocked manually before they can be drawn.
- Unlocking is permanent and persisted in save data.
- Unlocking the same tile twice is idempotent (no-op).

Current deterministic unlock cost table (existing enum names):
- `Plains` = `0` (default unlocked)
- `Woods` = `10`
- `River` = `20`
- `Meadow` = `30`
- `Village` = `40`
- `Lake` = `50`

## Determinism Guarantees
- `GameState` now includes the unlocked tile set in structural equality and hash code generation.
- `UnlockedTiles` is normalized to deterministic ordering for hashing and draw selection.
- Tile draw selection uses only the unlocked tile set.
- Given the same RNG state and the same unlocked set, draw results remain deterministic.

## Save Compatibility Rules
- Save schema version is bumped from Stage 13 to Stage 14 (`1` -> `2`).
- Stage 14 saves serialize `UnlockedTiles`.
- Loading Stage 13 saves (schema `1`) remains supported:
  - If `UnlockedTiles` is absent (legacy format), loader infers all tile types unlocked to preserve legacy behavior.
- Newer unsupported schema versions are rejected.

## Schema Version Bump Explanation
Stage 14 adds a new deterministic state field (`UnlockedTiles`) that directly changes gameplay outcomes (tile generation pool). This requires a schema version bump so the loader can distinguish:
- Legacy saves that implicitly assumed all tiles were unlocked
- Stage 14 saves that persist explicit unlock progression

## Testing Coverage Summary
Stage 14 test coverage verifies:
- Initial unlock state (`Plains` only)
- Unlock cost deduction
- Idempotent unlock behavior
- Unlock rejection when coins are insufficient
- Draws never produce locked tiles
- Save/load round-trip preserves unlocked tiles
- Stage 13 save compatibility infers all tiles unlocked
- `GameState` equality/hash include unlocked tiles
- Replay determinism remains stable after unlock actions
