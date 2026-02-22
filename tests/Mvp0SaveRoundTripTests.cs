using LandBuilder.Domain;
using LandBuilder.Infrastructure;

namespace LandBuilder.Tests;

public static class Mvp0SaveRoundTripTests
{
    public static void SaveLoadRoundTripPreservesState()
    {
        var savePath = Path.Combine(Path.GetTempPath(), "landbuilder_mvp1_test_save.json");

        var state = GameState.CreateMvp0Default();
        state = DeterministicSimulator.Apply(state, new PlaceBuildingCommand("Camp", 0)).State;
        state = DeterministicSimulator.Apply(state, new TickCommand(2)).State;
        state = DeterministicSimulator.Apply(state, new UpgradeBuildingCommand(1)).State;

        var repo = new SaveRepository();
        repo.Save(savePath, state);
        var loaded = repo.Load(savePath);

        if (loaded.Economy.Coins != state.Economy.Coins) throw new Exception("Coins mismatch after load");
        if (loaded.Buildings.Count != 1) throw new Exception("Building count mismatch");
        if (loaded.Buildings[1].Level != state.Buildings[1].Level) throw new Exception("Building level mismatch");
        if (loaded.Meta.SchemaVersion != 2) throw new Exception("Schema version mismatch");

        File.Delete(savePath);
    }
}
