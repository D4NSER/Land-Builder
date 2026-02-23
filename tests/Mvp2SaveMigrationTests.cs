using LandBuilder.Infrastructure;
using LandBuilder.Infrastructure.Content;
using Xunit;

namespace LandBuilder.Tests;

public class Mvp2SaveMigrationTests
{
    [Fact]
    public void V2SaveMigratesToV3Defaults()
    {
        var objectives = new ObjectiveDefinitionLoader().Load(Path.Combine("data", "objectives", "mvp2_objectives.json"));
        var repo = new SaveRepository(objectives);

        var fixturePath = Path.Combine("tests", "fixtures", "saves", "v2_mvp1_save.json");
        var loaded = repo.Load(fixturePath);

        Assert.Equal(3, loaded.Meta.SchemaVersion);
        Assert.Equal(0, loaded.Progression.CurrentObjectiveIndex);
        Assert.Empty(loaded.Progression.UnlockFlags);
    }
}
