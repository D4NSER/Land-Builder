namespace LandBuilder.Domain;

public interface IDomainEvent;

public sealed record CurrencySpentEvent(int Amount, int RemainingCoins) : IDomainEvent;
public sealed record CurrencyGainedEvent(int Amount, int TotalCoins) : IDomainEvent;
public sealed record TileUnlockedEvent(int TileId) : IDomainEvent;
public sealed record BuildingPlacedEvent(int BuildingId, string BuildingTypeId, int TileId) : IDomainEvent;
public sealed record BuildingUpgradedEvent(int BuildingId, int NewLevel) : IDomainEvent;
public sealed record UnlockFlagGrantedEvent(string UnlockFlag) : IDomainEvent;
public sealed record ObjectiveCompletedEvent(string ObjectiveId, string Message) : IDomainEvent;
public sealed record TickProcessedEvent(int StepCount) : IDomainEvent;
public sealed record CommandRejectedEvent(ValidationReasonCode ReasonCode, string Reason) : IDomainEvent;
