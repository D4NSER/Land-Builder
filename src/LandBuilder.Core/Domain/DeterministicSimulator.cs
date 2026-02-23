namespace LandBuilder.Domain;

public sealed class DeterministicSimulator
{
    private static readonly IReadOnlyList<TileDefinition> TileDeck = new List<TileDefinition>
    {
        new(TileType.Plains, EdgeType.Field, EdgeType.Field, EdgeType.Field, EdgeType.Field, 2, 1),
        new(TileType.Woods, EdgeType.Forest, EdgeType.Forest, EdgeType.Field, EdgeType.Field, 3, 1),
        new(TileType.River, EdgeType.Water, EdgeType.Water, EdgeType.Field, EdgeType.Field, 3, 2),
        new(TileType.Village, EdgeType.Town, EdgeType.Field, EdgeType.Field, EdgeType.Town, 4, 2),
        new(TileType.Meadow, EdgeType.Field, EdgeType.Forest, EdgeType.Field, EdgeType.Forest, 3, 1),
        new(TileType.Lake, EdgeType.Water, EdgeType.Field, EdgeType.Water, EdgeType.Field, 4, 2)
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

    private static (int X, int Y) ToXY(int slot) => (slot % GameState.BoardWidth, slot / GameState.BoardWidth);

    private static bool InBounds(int slot) => slot >= 0 && slot < GameState.BoardWidth * GameState.BoardHeight;

    private static IEnumerable<(int Slot, string Dir)> NeighborSlots(int slot)
    {
        var (x, y) = ToXY(slot);
        if (y > 0) yield return (slot - GameState.BoardWidth, "N");
        if (x < GameState.BoardWidth - 1) yield return (slot + 1, "E");
        if (y < GameState.BoardHeight - 1) yield return (slot + GameState.BoardWidth, "S");
        if (x > 0) yield return (slot - 1, "W");
    }

    private static (EdgeType N, EdgeType E, EdgeType S, EdgeType W) RotatedEdges(TileDefinition d, int r)
    {
        var rot = ((r % 4) + 4) % 4;
        return rot switch
        {
            0 => (d.North, d.East, d.South, d.West),
            1 => (d.West, d.North, d.East, d.South),
            2 => (d.South, d.West, d.North, d.East),
            _ => (d.East, d.South, d.West, d.North)
        };
    }

    private void ApplyDraw(GameState state, List<IDomainEvent> events)
    {
        var rng = NextRng(state.RngState);
        var index = (int)(rng % (ulong)TileDeck.Count);
        var tile = TileDeck[index].TileType;

        state.RngState = rng;
        state.RngStep += 1;
        state.CurrentTile = tile;
        state.LastMessage = $"Drew {tile}.";

        events.Add(new TileDrawnEvent(tile));
    }

    private void ApplyPlace(GameState state, PlaceTileCommand place, List<IDomainEvent> events)
    {
        if (!InBounds(place.SlotIndex))
        {
            Reject(state, events, place.SlotIndex, PlacementRejectionReason.OutOfBounds, "Slot is outside the board.");
            return;
        }

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
        var edges = RotatedEdges(def, place.RotationQuarterTurns);

        var matches = 0;
        foreach (var (nSlot, dir) in NeighborSlots(place.SlotIndex))
        {
            if (!state.Board.TryGetValue(nSlot, out var neighbor))
                continue;

            var nDef = GetDefinition(neighbor.TileType);
            var nEdges = RotatedEdges(nDef, neighbor.RotationQuarterTurns);

            var compatible = dir switch
            {
                "N" => edges.N == nEdges.S,
                "E" => edges.E == nEdges.W,
                "S" => edges.S == nEdges.N,
                _ => edges.W == nEdges.E
            };

            if (!compatible)
            {
                Reject(state, events, place.SlotIndex, PlacementRejectionReason.IncompatibleAdjacent, "Adjacent edges are incompatible.");
                return;
            }

            matches += 1;
        }

        var gain = def.BaseCoins + (matches * def.MatchBonusPerEdge);
        state.Board[place.SlotIndex] = new PlacedTile(state.CurrentTile.Value, ((place.RotationQuarterTurns % 4) + 4) % 4);
        state.Coins += gain;
        var placedType = state.CurrentTile.Value;
        state.CurrentTile = null;
        state.LastMessage = $"Placed {placedType} on slot {place.SlotIndex}. +{gain} coins.";

        events.Add(new TilePlacedEvent(place.SlotIndex, placedType, ((place.RotationQuarterTurns % 4) + 4) % 4, gain));
        events.Add(new CoinsEarnedEvent(gain, "tile_place"));
    }

    private static void Reject(GameState state, List<IDomainEvent> events, int slot, PlacementRejectionReason reason, string message)
    {
        state.LastMessage = message;
        events.Add(new PlacementRejectedEvent(slot, reason, message));
        events.Add(new CommandRejectedEvent(reason.ToString().ToUpperInvariant(), message));
    }
}
