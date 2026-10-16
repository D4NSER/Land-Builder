namespace LandBuilder.Domain;

/// <summary>All tunable numbers in one place. The balance tests replay bot runs against these values.</summary>
public static class GameRules
{
    public const int StartingStack = 32;
    public const int UndosPerRun = 3;

    public const int PointsPerMatchedEdge = 10;

    /// <summary>Every touching edge matches and at least this many neighbours touch the tile.</summary>
    public const int PerfectMinNeighbors = 3;
    public const int PerfectBonusPerNeighbor = 10;
    public const int PerfectTileReward = 1;

    /// <summary>All six neighbours present and matching.</summary>
    public const int FlawlessBonus = 100;
    public const int FlawlessTileReward = 2;

    public const int StreakMinMatches = 2;
    public const int StreakCap = 10;
    public const double StreakStep = 0.1;

    public const int MaxActiveQuests = 3;
    public const int FirstQuestTurn = 2;
    public const double QuestSpawnChance = 0.55;
    public const int MinQuestRegionSize = 2;

    public const int MilestoneTileReward = 4;

    /// <summary>Wonder cards come with every second milestone (the 2nd, 4th, ...) and every few quests.</summary>
    public static bool MilestoneAwardsWonder(int index) => index % 2 == 1;

    public const int QuestsPerWonder = 3;

    public static double MultiplierForStreak(int streak) => 1.0 + (Math.Min(streak, StreakCap) * StreakStep);

    /// <summary>Score needed for milestone <paramref name="index"/> (0-based). Gaps widen steadily.</summary>
    public static long MilestoneThreshold(int index)
    {
        long threshold = 0;
        for (var i = 0; i <= index; i++)
            threshold += 300 + (i * 175);
        return threshold;
    }

    /// <summary>Terrain unlocked by reaching milestone <paramref name="index"/> on an island of the given theme, if any.</summary>
    public static Terrain? MilestoneUnlock(WorldTheme theme, int index) => Themes.Unlock(theme, index);
}
