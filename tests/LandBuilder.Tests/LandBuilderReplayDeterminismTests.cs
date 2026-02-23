using LandBuilder.Application;
using LandBuilder.Domain;
using LandBuilder.Infrastructure;
using Xunit;

namespace LandBuilder.Tests;

public class LandBuilderReplayDeterminismTests
{
    [Fact]
    public void SaveLoadMidRun_ContinuedCommands_MatchControlRunExactly()
    {
        var seed = 20260223UL;
        var preSaveCommands = new IGameCommand[]
        {
            new DrawTileCommand(),
            new PlaceTileCommand(0),
            new DrawTileCommand(),
            new PlaceTileCommand(1),
            new DrawTileCommand(),
            new PlaceTileCommand(3)
        };

        var postSaveCommands = new IGameCommand[]
        {
            new DrawTileCommand(),
            new PlaceTileCommand(4),
            new DrawTileCommand(),
            new PlaceTileCommand(2)
        };

        var control = new GameSession(GameState.CreateInitial(seed), new InMemoryEventSink());
        foreach (var c in preSaveCommands) control.IssueCommand(c);
        foreach (var c in postSaveCommands) control.IssueCommand(c);

        var replay = new GameSession(GameState.CreateInitial(seed), new InMemoryEventSink());
        foreach (var c in preSaveCommands) replay.IssueCommand(c);

        var path = Path.Combine(Path.GetTempPath(), $"landbuilder-replay-{Guid.NewGuid():N}.json");
        try
        {
            var repo = new SaveRepository();
            repo.Save(path, replay.State);
            Assert.True(repo.TryLoad(path, out var loaded, out var error), error);

            replay = new GameSession(loaded, new InMemoryEventSink());
            foreach (var c in postSaveCommands) replay.IssueCommand(c);

            Assert.Equal(control.State, replay.State);
            Assert.Equal(control.State.GetHashCode(), replay.State.GetHashCode());
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
            if (File.Exists(path + ".bak")) File.Delete(path + ".bak");
            if (File.Exists(path + ".tmp")) File.Delete(path + ".tmp");
        }
    }
}
