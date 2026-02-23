using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
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
            Assert.Equal(session.State.GetHashCode(), loaded.GetHashCode());
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

    [Fact]
    public void TryLoad_UnsupportedSchema_ReturnsFalseAndError()
    {
        var path = Path.Combine(Path.GetTempPath(), $"landbuilder-save-schema-{Guid.NewGuid():N}.json");
        try
        {
            var repo = new SaveRepository();
            repo.Save(path, GameState.CreateInitial());

            var root = JsonNode.Parse(File.ReadAllText(path))?.AsObject()
                ?? throw new InvalidDataException("Expected JSON object payload.");
            var schemaKey = root.ContainsKey("SchemaVersion")
                ? "SchemaVersion"
                : root.ContainsKey("schemaVersion")
                    ? "schemaVersion"
                    : root.FirstOrDefault(kvp =>
                        kvp.Key.Contains("schema", StringComparison.OrdinalIgnoreCase) ||
                        kvp.Key.Contains("version", StringComparison.OrdinalIgnoreCase)).Key;

            if (string.IsNullOrWhiteSpace(schemaKey))
                throw new InvalidDataException("Could not locate schema/version field in save payload.");

            root[schemaKey] = 999;
            File.WriteAllText(path, root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));

            var ok = repo.TryLoad(path, out var loaded, out var error);

            Assert.False(ok);
            Assert.Contains("Unsupported", error, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public void Load_InvalidSave_ThrowsInvalidDataException()
    {
        var path = Path.Combine(Path.GetTempPath(), $"landbuilder-save-invalid-{Guid.NewGuid():N}.json");
        try
        {
            File.WriteAllText(path, "{ not-json }");
            var repo = new SaveRepository();

            var ex = Assert.Throws<InvalidDataException>(() => repo.Load(path));
            Assert.True(
                ex.Message.Contains("invalid", StringComparison.OrdinalIgnoreCase) ||
                ex.Message.Contains("corrupt", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }
}
