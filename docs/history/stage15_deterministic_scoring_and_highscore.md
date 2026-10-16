# Stage 15: Deterministic Scoring + High Score Persistence

## Overview and Motivation
Stage 15 adds a deterministic scoring system derived entirely from the board layout, plus a persistent high score stored in a separate file. This keeps score reproducible from game state while allowing long-term progress tracking across sessions.

## Scoring Rules
Score is computed from board occupancy and tile types only (rotation does not affect score).

### Base Points per Placed Tile
- `Plains` = `1`
- `Woods` = `2`
- `River` = `3`
- `Meadow` = `4`
- `Village` = `5`
- `Lake` = `6`

### Adjacency Bonus (4-neighborhood)
- For each adjacent pair (up/down/left/right) of placed tiles with the same `TileType`, add `+2`.
- Each pair is counted once only (right and down neighbors only).

### Full Board Bonus
- If all `3x3` board slots are filled, add `+10`.

## Determinism Notes
- `GameState.Score` is computed (not mutable persisted runtime state).
- Score depends only on placed tile positions/types.
- Adjacency counting uses a stable one-pass neighbor policy (right/down only), preventing double counting.
- Replay determinism remains intact: identical command sequences from identical initial state produce identical final state including score.

## Game Save Schema v3 and Backward Compatibility
- Game save schema version is bumped to `3`.
- Stage 15 saves include `Score` for debugging/inspection.
- On load:
  - Schema `3`: `Score` is optional; if present, it must match recomputed score or load is rejected.
  - Schema `2` (Stage 14): no `Score`; score is recomputed, `UnlockedTiles` is loaded normally.
  - Schema `1` (Stage 13): no `UnlockedTiles`, no `Score`; loader infers all tiles unlocked and recomputes score.
- Unsupported newer schemas are rejected.

## High Score Persistence Format and Validation
- High score is stored separately from the game save.
- JSON format:
  - `{ "SchemaVersion": 1, "HighScore": <int> }`
- Validation rules:
  - Missing file => treated as high score `0`
  - Corrupt JSON => rejected (`TryLoadHighScore` returns false, `LoadHighScore` throws)
  - Unsupported schema => rejected

## Testing Coverage Summary
Stage 15 test coverage verifies:
- Scoring for empty board, base tile values, adjacency pair counting, and full-board bonus
- Game save schema `3` score validation and rejection on mismatch
- Backward compatibility for game save schemas `2` and `1`
- High score repository defaults, round-trip persistence, and corruption handling
- `SubmitScoreCommand` updates high score only when the submitted score is higher
- Replay determinism preserves final score
