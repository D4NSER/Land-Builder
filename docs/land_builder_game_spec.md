# Land Builder Game Spec (Reset)

Land Builder is a deterministic tile-placement game loop: the player draws random tiles from a seeded RNG, places them onto a board, and earns coins from valid placement plus edge matches. The same seed and command sequence always produce the same board and coin outcome.

## Core loop
- Draw tile (`DrawTileCommand`)
- Inspect current tile in hand
- Place tile onto one board slot (`PlaceTileCommand`)
- If placement is valid, earn coins; if invalid, receive deterministic rejection reason
- Save/load preserves board, coins, tile in hand, and RNG progression

## Tile/matching rule
- Board is a fixed 3x3 grid (`slots 0..8`).
- Each tile has 4 edge types: North/East/South/West (`Field`, `Forest`, `Water`, `Town`).
- Placement is valid only when all existing adjacent neighbors have matching opposite edge types.
- Coin reward = `BaseCoins + (AdjacentMatches * MatchBonusPerEdge)`.
- Coins are never spent to place tiles.

## Commands / events
- Commands:
  - `DrawTileCommand`
  - `PlaceTileCommand(slotIndex, rotationQuarterTurns)`
- Events:
  - `TileDrawnEvent`
  - `TilePlacedEvent`
  - `PlacementRejectedEvent`
  - `CoinsEarnedEvent`
  - `CommandRejectedEvent`

## Save schema fields
- `SchemaVersion`
- `Coins`
- `RngState`
- `RngStep`
- `CurrentTile`
- `Board[]` with `SlotIndex`, `TileType`, `RotationQuarterTurns`
- `LastMessage`

## Run tests (CLI)
```powershell
dotnet test .\LandBuilder.sln
```

## Run in Godot (Windows)
1. Open project in Godot 4.2.2 .NET.
2. If prompted, use **Project -> Tools -> C# -> Create C# Solution**.
3. Run **Build -> Build Project**.
4. Open/run `scenes/main.tscn`.
