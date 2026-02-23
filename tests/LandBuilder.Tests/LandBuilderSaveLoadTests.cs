using LandBuilder.Application;
using LandBuilder.Domain;
using LandBuilder.Infrastructure;
using Xunit;

namespace LandBuilder.Tests;

public class LandBuilderSaveLoadTests
{
    [Fact]
    public void SaveLoad_RoundTripPreservesFullState()
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
            Assert.True(repo.TryLoad(path, out var loaded, out var error), error);

            Assert.Equal(session.State, loaded);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
            if (File.Exists(path + ".bak")) File.Delete(path + ".bak");
            if (File.Exists(path + ".tmp")) File.Delete(path + ".tmp");
        }
    }

    [Fact]
    public void TryLoad_CorruptedSave_ReturnsFalseAndError()
    {
        var path = Path.Combine(Path.GetTempPath(), $"landbuilder-save-corrupt-{Guid.NewGuid():N}.json");
        try
        {
            File.WriteAllText(path, "{ not-json }");
            var repo = new SaveRepository();

            var ok = repo.TryLoad(path, out var loaded, out var error);

            Assert.False(ok);
            Assert.NotEmpty(error);
            Assert.Equal(GameState.CreateInitial(), loaded);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }
}
