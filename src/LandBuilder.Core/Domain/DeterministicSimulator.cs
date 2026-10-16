namespace LandBuilder.Domain;

public sealed class DeterministicSimulator
{
    private static readonly IReadOnlyList<TileDefinition> TileDeck = new List<TileDefinition>
    {
        new(TileType.Plains, EdgeType.Plains, 0, 1),
        new(TileType.Woods, EdgeType.Forest, 0, 1),
        new(TileType.River, EdgeType.Beach, 0, 1),
        new(TileType.Village, EdgeType.City, 0, 1),
        new(TileType.Meadow, EdgeType.Farmland, 0, 1),
        new(TileType.Lake, EdgeType.Mountain, 0, 1),
        new(TileType.Desert, EdgeType.Desert, 0, 1),
        new(TileType.Snow, EdgeType.Snow, 0, 1),
        new(TileType.Volcano, EdgeType.Mountain, 0, 1),
        new(TileType.Pond, EdgeType.Water, 0, 1),
        new(TileType.Ruins, EdgeType.City, 0, 1),
        new(TileType.Swamp, EdgeType.Swamp, 0, 1),
        new(TileType.Savanna, EdgeType.Savanna, 0, 1),
        new(TileType.Tundra, EdgeType.Tundra, 0, 1),
        new(TileType.Coral, EdgeType.Coral, 0, 1),
        new(TileType.Mushroom, EdgeType.Mushroom, 0, 1)
    };

    public (GameState NextState, IReadOnlyList<IDomainEvent> Events) Apply(GameState state, IGameCommand command)
    {
        var working = state.Clone();
        var events = new List<IDomainEvent>();

        switch (command)
        {
            case DrawTileCommand:
                ApplyDraw(working, events);
                break;
            case SubmitScoreCommand:
                ApplySubmitScore(working, events);
                break;
            case UnlockTileCommand unlock:
                ApplyUnlock(working, unlock, events);
                break;
            case PlaceTileCommand place:
                ApplyPlace(working, place, events);
                break;
            default:
                events.Add(new CommandRejectedEvent("UNKNOWN_COMMAND", "Unsupported command."));
                working.LastMessage = "Unsupported command.";
                break;
        }

        return (working, events);
    }

    public TileDefinition GetDefinition(TileType type) => TileDeck[(int)type];

    private static ulong NextRng(ulong x)
    {
        x ^= x >> 12;
        x ^= x << 25;
        x ^= x >> 27;
        return x * 2685821657736338717UL;
    }

    private void ApplyDraw(GameState state, List<IDomainEvent> events)
    {
        var unlocked = state.UnlockedTiles.OrderBy(x => x).ToArray();
        if (unlocked.Length == 0)
        {
            events.Add(new CommandRejectedEvent("NO_UNLOCKED_TILES", "No tiles are unlocked."));
            state.LastMessage = "No tiles are unlocked.";
            return;
        }

        var rng = NextRng(state.RngState);
        var index = (int)(rng % (ulong)unlocked.Length);
        var tile = unlocked[index];

        state.RngState = rng;
        state.RngStep += 1;
        state.CurrentTile = tile;
        state.LastMessage = $"Drew {tile}.";

        events.Add(new TileDrawnEvent(tile));
    }

    private static void ApplyUnlock(GameState state, UnlockTileCommand unlock, List<IDomainEvent> events)
    {
        try
        {
            var next = state.Unlock(unlock.TileType);
            if (ReferenceEquals(next, state))
            {
                state.LastMessage = $"{unlock.TileType} is already unlocked.";
                return;
            }

            state.ApplySnapshotFrom(next);
        }
        catch (InvalidOperationException ex)
        {
            state.LastMessage = ex.Message;
            events.Add(new CommandRejectedEvent("INSUFFICIENT_COINS", ex.Message));
        }
    }

    private static void ApplySubmitScore(GameState state, List<IDomainEvent> events)
    {
        state.LastMessage = $"Submitted score {state.Score}.";
        events.Add(new ScoreSubmittedEvent(state.Score));
    }

    private void ApplyPlace(GameState state, PlaceTileCommand place, List<IDomainEvent> events)
    {
        if (state.CurrentTile is null)
        {
            Reject(state, events, place.SlotIndex, PlacementRejectionReason.NoTileInHand, "Draw a tile before placement.");
            return;
        }

        if (state.Board.ContainsKey(place.SlotIndex))
        {
            Reject(state, events, place.SlotIndex, PlacementRejectionReason.SlotOccupied, "Slot already has a tile.");
            return;
        }

        var def = GetDefinition(state.CurrentTile.Value);
        var sectorPattern = TileSectorGenerator.Generate(state.CurrentTile.Value, state.RngState, state.ThemeContext);
        state.RngState = NextRng(state.RngState);
        var adjacentTiles = 0;
        var matches = 0;
        foreach (var neighborSlot in HexGrid.GetNeighborSlots(place.SlotIndex))
        {
            if (!state.Board.TryGetValue(neighborSlot, out var neighbor))
                continue;

            adjacentTiles += 1;
            var sourceEdge = GameState.GetEdgeIndexFacingNeighbor(place.SlotIndex, neighborSlot);
            var neighborEdge = GameState.GetEdgeIndexFacingNeighbor(neighborSlot, place.SlotIndex);
            if (sourceEdge < 0 || neighborEdge < 0)
                continue;

            var sourceTerrain = sectorPattern.GetEdgeTerrain(sourceEdge, place.RotationQuarterTurns);
            var neighborTerrain = neighbor.ResolvedSectorPattern.GetEdgeTerrain(neighborEdge, neighbor.RotationQuarterTurns);
            if (GameState.AreTerrainTypesCompatible(sourceTerrain, neighborTerrain))
                matches += 1;
        }

        if (state.Board.Count > 0 && adjacentTiles == 0)
        {
            Reject(state, events, place.SlotIndex, PlacementRejectionReason.IncompatibleAdjacent, "Tile must connect to the existing island.");
            return;
        }

        var gain = def.BaseCoins + (matches * def.MatchBonusPerEdge);
        var normalizedRotation = ((place.RotationQuarterTurns % 6) + 6) % 6;
        state.Board[place.SlotIndex] = new PlacedTile(state.CurrentTile.Value, normalizedRotation, sectorPattern);
        state.Coins += gain;
        var placedType = state.CurrentTile.Value;
        state.CurrentTile = null;
        state.LastMessage = $"Placed {placedType} on slot {place.SlotIndex}. +{gain} coins.";

        events.Add(new TilePlacedEvent(place.SlotIndex, placedType, normalizedRotation, gain));
        events.Add(new CoinsEarnedEvent(gain, "tile_place"));
    }
    private static void Reject(GameState state, List<IDomainEvent> events, int slot, PlacementRejectionReason reason, string message)
    {
        state.LastMessage = message;
        events.Add(new PlacementRejectedEvent(slot, reason, message));
        events.Add(new CommandRejectedEvent(reason.ToString().ToUpperInvariant(), message));
    }
}
