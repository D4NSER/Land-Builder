using LandBuilder.Application;
using LandBuilder.Domain;
using LandBuilder.Infrastructure;
using Xunit;

namespace LandBuilder.Tests;

public class LandBuilderSaveLoadTests
{
    [Fact]
    public void SaveLoad_RoundTripPreservesCoinsBoardAndRng()
    {
        var session = new GameSession(GameState.CreateInitial(4242), new InMemoryEventSink());
        session.IssueCommand(new DrawTileCommand());
        session.IssueCommand(new PlaceTileCommand(0));
        session.IssueCommand(new DrawTileCommand());
        session.IssueCommand(new PlaceTileCommand(1));

        var path = Path.Combine(Path.GetTempPath(), $"landbuilder-save-{Guid.NewGuid():N}.json");
        try
        {
            var repo = new SaveRepository();
            repo.Save(path, session.State);
            var loaded = repo.Load(path);

            Assert.Equal(session.State.Coins, loaded.Coins);
            Assert.Equal(session.State.RngState, loaded.RngState);
            Assert.Equal(session.State.RngStep, loaded.RngStep);
            Assert.Equal(session.State.CurrentTile, loaded.CurrentTile);
            Assert.Equal(session.State.Board.Count, loaded.Board.Count);
            foreach (var kv in session.State.Board)
            {
                Assert.True(loaded.Board.ContainsKey(kv.Key));
                Assert.Equal(kv.Value, loaded.Board[kv.Key]);
            }
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
            if (File.Exists(path + ".bak")) File.Delete(path + ".bak");
            if (File.Exists(path + ".tmp")) File.Delete(path + ".tmp");
        }
    }
}
