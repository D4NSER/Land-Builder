namespace LandBuilder.Domain;

public interface IDomainEvent;

public sealed record TileDrawnEvent(TileType TileType) : IDomainEvent;

public sealed record TilePlacedEvent(int SlotIndex, TileType TileType, int RotationQuarterTurns, long CoinsEarned) : IDomainEvent;

public sealed record CoinsEarnedEvent(long Amount, string Reason) : IDomainEvent;

public sealed record PlacementRejectedEvent(int SlotIndex, PlacementRejectionReason Reason, string Message) : IDomainEvent;

public sealed record CommandRejectedEvent(string ReasonCode, string Message) : IDomainEvent;

public sealed record ScoreSubmittedEvent(int Score) : IDomainEvent;

public sealed record HighScoreUpdatedEvent(int PreviousHighScore, int NewHighScore) : IDomainEvent;
