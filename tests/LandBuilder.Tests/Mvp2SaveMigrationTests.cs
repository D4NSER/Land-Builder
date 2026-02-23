using LandBuilder.Infrastructure;
using LandBuilder.Infrastructure.Content;
using Xunit;

namespace LandBuilder.Tests;

public class Mvp2SaveMigrationTests
{
    [Fact]
    public void V2SaveMigratesToV3Defaults()
    {
        var objectives = new ObjectiveDefinitionLoader().Load(TestPaths.ObjectivesJson);
        var repo = new SaveRepository(objectives);

        var fixturePath = TestPaths.V2Fixture;
        var loaded = repo.Load(fixturePath);

        Assert.Equal(3, loaded.Meta.SchemaVersion);
        Assert.Equal(0, loaded.Progression.CurrentObjectiveIndex);
        Assert.Empty(loaded.Progression.UnlockFlags);
    }
}
