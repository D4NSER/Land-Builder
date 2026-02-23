namespace LandBuilder.Domain;

public interface IGameCommand;

public sealed record DrawTileCommand() : IGameCommand;

public sealed record PlaceTileCommand(int SlotIndex, int RotationQuarterTurns = 0) : IGameCommand;

public sealed record UnlockTileCommand(TileType TileType) : IGameCommand;

public sealed record SubmitScoreCommand() : IGameCommand;
