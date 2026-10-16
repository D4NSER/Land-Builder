namespace LandBuilder.Domain;

public interface IDomainEvent;

public enum PlacementQuality
{
    Normal = 0,
    Perfect = 1,
    Flawless = 2
}

public enum PlacementRejectionReason
{
    GameOver = 0,
    NoTileInHand = 1,
    SlotOccupied = 2,
    NotConnected = 3,
    RiverBlocked = 4,
    NoWonderCard = 5
}

/// <summary>Full breakdown of what a placement is worth. Also used for hover previews before committing.</summary>
public sealed record PlacementEvaluation(
    int Slot,
    int Rotation,
    bool IsValid,
    PlacementRejectionReason? RejectionReason,
    IReadOnlyList<int> MatchedEdges,
    IReadOnlyList<int> MismatchedEdges,
    int NeighborCount,
    int BasePoints,
    double Multiplier,
    int EdgePoints,
    PlacementQuality Quality,
    int BonusPoints,
    int TilesAwarded)
{
    public int TotalPoints => EdgePoints + BonusPoints;

    /// <summary>Clean fits touching two or more tiles grow the streak.</summary>
    public bool BuildsStreak => MismatchedEdges.Count == 0 && MatchedEdges.Count >= GameRules.StreakMinMatches;

    /// <summary>Any mismatched edge resets the streak; single clean matches just hold it.</summary>
    public bool BreaksStreak => MismatchedEdges.Count > 0;
}

public sealed record TilePlacedEvent(int Slot, TileSpec Tile, int Rotation, PlacementEvaluation Evaluation) : IDomainEvent;

public sealed record PlacementRejectedEvent(int Slot, PlacementRejectionReason Reason, string Message) : IDomainEvent;

public sealed record StreakChangedEvent(int Previous, int Current, double Multiplier) : IDomainEvent;

public sealed record QuestStartedEvent(Quest Quest, int CurrentSize) : IDomainEvent;

public sealed record QuestCompletedEvent(Quest Quest, int FinalSize) : IDomainEvent;

public enum QuestFailReason
{
    RegionClosed = 0,
    Overshot = 1
}

public sealed record QuestFailedEvent(Quest Quest, QuestFailReason Reason, int FinalSize) : IDomainEvent;

public sealed record MilestoneReachedEvent(int Index, long Threshold, int TilesAwarded, Terrain? UnlockedTerrain) : IDomainEvent;

public sealed record TilesAwardedEvent(int Amount, string Reason) : IDomainEvent;

public sealed record HandAdvancedEvent(TileSpec? Current, TileSpec? Next, int StackCount) : IDomainEvent;

/// <summary>The tile in hand fit nowhere (rivers cannot connect), so it was set aside and the next one drawn.</summary>
public sealed record TileDiscardedEvent(TileSpec Tile) : IDomainEvent;

public sealed record WonderAwardedEvent(WonderType Type, string Reason) : IDomainEvent;

public sealed record WonderPlacedEvent(int Slot, WonderType Type, PlacementEvaluation Evaluation) : IDomainEvent;

public sealed record WonderCompletedEvent(int Slot, WonderType Type, int Points, int Tiles) : IDomainEvent;

public sealed record GameOverEvent(long FinalScore, RunStats Stats) : IDomainEvent;

public sealed record HighScoreUpdatedEvent(long PreviousHighScore, long NewHighScore) : IDomainEvent;
