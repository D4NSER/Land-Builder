namespace LandBuilder.Domain;

public interface IDomainEvent;

public sealed record CurrencySpentEvent(int Amount, int RemainingCoins) : IDomainEvent;
public sealed record TileUnlockedEvent(int TileId) : IDomainEvent;
public sealed record TickProcessedEvent(int StepCount) : IDomainEvent;
public sealed record CommandRejectedEvent(string Reason) : IDomainEvent;
