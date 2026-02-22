namespace LandBuilder.Domain;

public interface IGameCommand;

public sealed record ExpandTileCommand(int TileId) : IGameCommand;
public sealed record PlaceBuildingCommand(string BuildingTypeId, int TileId) : IGameCommand;
public sealed record UpgradeBuildingCommand(int BuildingId) : IGameCommand;
public sealed record TickCommand(int Steps = 1) : IGameCommand;
