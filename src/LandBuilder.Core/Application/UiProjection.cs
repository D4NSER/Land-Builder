using LandBuilder.Domain;

namespace LandBuilder.Application;

public sealed record SlotProjection(int SlotIndex, string Occupant);

public sealed record UiProjection(
    long Coins,
    string CurrentTile,
    IReadOnlyList<SlotProjection> Slots,
    string LastMessage
);

public static class UiProjectionBuilder
{
    public static UiProjection Build(GameState state)
    {
        var slots = new List<SlotProjection>();
        for (var i = 0; i < GameState.BoardWidth * GameState.BoardHeight; i++)
        {
            if (state.Board.TryGetValue(i, out var placed))
                slots.Add(new SlotProjection(i, $"{placed.TileType} (r{placed.RotationQuarterTurns})"));
            else
                slots.Add(new SlotProjection(i, "Empty"));
        }

        return new UiProjection(
            state.Coins,
            state.CurrentTile?.ToString() ?? "(none)",
            slots,
            state.LastMessage
        );
    }
}
