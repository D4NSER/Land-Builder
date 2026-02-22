using LandBuilder.Domain;
using LandBuilder.Infrastructure;
using LandBuilder.Infrastructure.Content;

namespace LandBuilder.Tests;

public static class Mvp0SaveRoundTripTests
{
    public static void SaveLoadRoundTripPreservesState()
    {
        var savePath = Path.Combine(Path.GetTempPath(), "landbuilder_mvp2_test_save.json");
        var objectives = new ObjectiveDefinitionLoader().Load(Path.Combine("data", "objectives", "mvp2_objectives.json"));

        var state = GameState.CreateInitial(objectives);
        state = DeterministicSimulator.Apply(state, new ExpandTileCommand(1)).State;
        state = DeterministicSimulator.Apply(state, new PlaceBuildingCommand("Camp", 0)).State;
        state = DeterministicSimulator.Apply(state, new TickCommand(3)).State;

        var repo = new SaveRepository(objectives);
        repo.Save(savePath, state);
        var loaded = repo.Load(savePath);

        if (loaded.Economy.Coins != state.Economy.Coins) throw new Exception("Coins mismatch after load");
        if (loaded.Buildings.Count != state.Buildings.Count) throw new Exception("Building count mismatch");
        if (loaded.Progression.CurrentObjectiveIndex != state.Progression.CurrentObjectiveIndex) throw new Exception("Progression index mismatch");
        if (loaded.Meta.SchemaVersion != 3) throw new Exception("Schema version mismatch");

        File.Delete(savePath);
    }
}
