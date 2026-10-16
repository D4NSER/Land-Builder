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
        var slots = state.Board
            .OrderBy(x => x.Key)
            .Select(x => new SlotProjection(x.Key, $"{x.Value.TileType} (r{x.Value.RotationQuarterTurns})"))
            .ToList();

        return new UiProjection(
            state.Coins,
            state.CurrentTile?.ToString() ?? "(none)",
            slots,
            state.LastMessage
        );
    }
}
