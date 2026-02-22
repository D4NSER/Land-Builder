using LandBuilder.Infrastructure;
using LandBuilder.Infrastructure.Content;

namespace LandBuilder.Tests;

public static class Mvp2SaveMigrationTests
{
    public static void V2SaveMigratesToV3Defaults()
    {
        var objectives = new ObjectiveDefinitionLoader().Load(Path.Combine("data", "objectives", "mvp2_objectives.json"));
        var repo = new SaveRepository(objectives);

        var fixturePath = Path.Combine("tests", "fixtures", "saves", "v2_mvp1_save.json");
        var loaded = repo.Load(fixturePath);

        if (loaded.Meta.SchemaVersion != 3) throw new Exception("Expected migrated schema version 3");
        if (loaded.Progression.CurrentObjectiveIndex != 0) throw new Exception("Expected default objective index 0 after migration");
        if (loaded.Progression.UnlockFlags.Count != 0) throw new Exception("Expected no unlock flags after migration default");
    }
}
