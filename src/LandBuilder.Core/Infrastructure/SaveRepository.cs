using System.Text.Json;
using System.Text.Json.Serialization;
using LandBuilder.Domain;

namespace LandBuilder.Infrastructure;

/// <summary>
/// Persists an in-progress run. Schema 6 adds multi-tile wonder blueprints. Schema 5 saves still load (their old
/// single-tile wonders become plain tiles); older schemas belonged to retired prototypes and are rejected.
/// </summary>
public sealed class SaveRepository
{
    public const int CurrentSchemaVersion = 6;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public void Save(string path, GameState state)
    {
        var payload = new SavePayload
        {
            SchemaVersion = CurrentSchemaVersion,
            Seed = state.Seed,
            Theme = state.Theme,
            RngState = state.RngState,
            Score = state.Score,
            Streak = state.Streak,
            StackCount = state.StackCount,
            CurrentTile = state.CurrentTile?.ToArray(),
            NextTile = state.NextTile?.ToArray(),
            NextQuestId = state.NextQuestId,
            MilestonesReached = state.MilestonesReached,
            UndosRemaining = state.UndosRemaining,
            IsGameOver = state.IsGameOver,
            WonderCards = state.WonderCards.ToList(),
            NextSiteId = state.NextSiteId,
            Sites = state.Sites.Select(s => new SaveSite { Id = s.Id, Type = s.Type, Anchor = s.Anchor, Rotation = s.Rotation, Turn = s.Turn, Built = s.Built }).ToList(),
            Unlocked = state.Unlocked.ToList(),
            Stats = state.Stats,
            Quests = state.Quests.Select(q => new SaveQuest
            {
                Id = q.Id,
                Terrain = q.Terrain,
                Kind = q.Kind,
                Target = q.Target,
                AnchorSlot = q.Anchor.Slot,
                AnchorEdge = q.Anchor.Edge,
                RewardTiles = q.RewardTiles,
                RewardPoints = q.RewardPoints,
                CreatedTurn = q.CreatedTurn
            }).ToList(),
            Board = state.Board.OrderBy(kv => kv.Key).Select(kv => new SaveTile
            {
                Slot = kv.Key,
                Sectors = kv.Value.Tile.ToArray(),
                Rotation = kv.Value.Rotation,
                Turn = kv.Value.Turn
            }).ToList()
        };

        AtomicFile.WriteAllText(path, JsonSerializer.Serialize(payload, JsonOptions));
    }

    public bool TryLoad(string path, out GameState state, out string error)
    {
        state = GameState.CreateInitial(1);
        error = string.Empty;

        if (!File.Exists(path))
        {
            error = "Save file not found.";
            return false;
        }

        SavePayload? payload;
        try
        {
            payload = JsonSerializer.Deserialize<SavePayload>(File.ReadAllText(path), JsonOptions);
        }
        catch (Exception ex)
        {
            error = $"Save is corrupted or unreadable: {ex.Message}";
            return false;
        }

        if (payload is null)
        {
            error = "Save payload is empty.";
            return false;
        }

        if (payload.SchemaVersion is not (5 or CurrentSchemaVersion))
        {
            error = $"Unsupported save schema: {payload.SchemaVersion}.";
            return false;
        }

        try
        {
            state = new GameState
            {
                Seed = payload.Seed,
                Theme = payload.Theme ?? WorldTheme.Temperate,
                RngState = payload.RngState,
                Score = payload.Score,
                Streak = payload.Streak,
                StackCount = payload.StackCount,
                CurrentTile = payload.CurrentTile is null ? null : TileSpec.FromArray(payload.CurrentTile),
                NextTile = payload.NextTile is null ? null : TileSpec.FromArray(payload.NextTile),
                NextQuestId = payload.NextQuestId,
                MilestonesReached = payload.MilestonesReached,
                UndosRemaining = payload.UndosRemaining,
                IsGameOver = payload.IsGameOver,
                WonderCards = payload.WonderCards ?? new List<WonderType>(),
                NextSiteId = Math.Max(1, payload.NextSiteId),
                Sites = (payload.Sites ?? new List<SaveSite>()).Select(s => new WonderSite(s.Id, s.Type, s.Anchor, GameEngine.NormalizeRotation(s.Rotation), s.Turn, s.Built)).ToList(),
                Unlocked = payload.Unlocked ?? Terrains.Starting.ToList(),
                Stats = payload.Stats ?? RunStats.Empty,
                Quests = (payload.Quests ?? new List<SaveQuest>()).Select(q => new Quest(
                    q.Id, q.Terrain, q.Kind, q.Target, new SectorRef(q.AnchorSlot, q.AnchorEdge), q.RewardTiles, q.RewardPoints, q.CreatedTurn)).ToList(),
                Board = (payload.Board ?? new List<SaveTile>()).ToDictionary(
                    t => t.Slot,
                    t => new PlacedTile(TileSpec.FromArray(t.Sectors ?? throw new InvalidDataException("Tile without sectors.")), GameEngine.NormalizeRotation(t.Rotation), t.Turn))
            };
        }
        catch (Exception ex)
        {
            error = $"Save is invalid: {ex.Message}";
            state = GameState.CreateInitial(1);
            return false;
        }

        if (state.Board.Count == 0)
        {
            error = "Save is invalid: empty board.";
            state = GameState.CreateInitial(1);
            return false;
        }

        return true;
    }

    public GameState Load(string path)
    {
        if (TryLoad(path, out var state, out var error))
            return state;

        throw new InvalidDataException(error);
    }

    public void Delete(string path)
    {
        if (File.Exists(path))
            File.Delete(path);
    }

    private sealed class SavePayload
    {
        public int SchemaVersion { get; set; }
        public ulong Seed { get; set; }
        public WorldTheme? Theme { get; set; }
        public ulong RngState { get; set; }
        public long Score { get; set; }
        public int Streak { get; set; }
        public int StackCount { get; set; }
        public Terrain[]? CurrentTile { get; set; }
        public Terrain[]? NextTile { get; set; }
        public int NextQuestId { get; set; }
        public int MilestonesReached { get; set; }
        public int UndosRemaining { get; set; }
        public bool IsGameOver { get; set; }
        public List<WonderType>? WonderCards { get; set; }
        public int NextSiteId { get; set; }
        public List<SaveSite>? Sites { get; set; }
        public List<Terrain>? Unlocked { get; set; }
        public RunStats? Stats { get; set; }
        public List<SaveQuest>? Quests { get; set; }
        public List<SaveTile>? Board { get; set; }
    }

    private sealed class SaveQuest
    {
        public int Id { get; set; }
        public Terrain Terrain { get; set; }
        public QuestKind Kind { get; set; }
        public int Target { get; set; }
        public int AnchorSlot { get; set; }
        public int AnchorEdge { get; set; }
        public int RewardTiles { get; set; }
        public int RewardPoints { get; set; }
        public int CreatedTurn { get; set; }
    }

    private sealed class SaveTile
    {
        public int Slot { get; set; }
        public Terrain[]? Sectors { get; set; }
        public int Rotation { get; set; }
        public int Turn { get; set; }
    }

    private sealed class SaveSite
    {
        public int Id { get; set; }
        public WonderType Type { get; set; }
        public int Anchor { get; set; }
        public int Rotation { get; set; }
        public int Turn { get; set; }
        public bool Built { get; set; }
    }
}

internal static class AtomicFile
{
    public static void WriteAllText(string path, string contents)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        var temp = path + ".tmp";
        File.WriteAllText(temp, contents);
        if (File.Exists(path))
            File.Replace(temp, path, path + ".bak", true);
        else
            File.Move(temp, path);
    }
}
