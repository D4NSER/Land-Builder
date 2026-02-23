namespace LandBuilder.Domain;

public interface IGameCommand;

public sealed record DrawTileCommand() : IGameCommand;

public sealed record PlaceTileCommand(int SlotIndex, int RotationQuarterTurns = 0) : IGameCommand;
