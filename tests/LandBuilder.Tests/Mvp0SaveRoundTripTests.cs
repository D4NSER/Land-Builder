using LandBuilder.Domain;
using LandBuilder.Infrastructure;

namespace LandBuilder.Tests;

public static class Mvp0SaveRoundTripTests
{
    public static void SaveLoadRoundTripPreservesState()
    {
        var savePath = Path.Combine(Path.GetTempPath(), "landbuilder_mvp0_test_save.json");

        var state = GameState.CreateMvp0Default();
        state = DeterministicSimulator.Apply(state, new ExpandTileCommand(1)).State;

        var repo = new SaveRepository();
        repo.Save(savePath, state);
        var loaded = repo.Load(savePath);

        if (loaded.Economy.Coins != state.Economy.Coins) throw new Exception("Coins mismatch after load");
        if (loaded.World.Tiles[1].Ownership != state.World.Tiles[1].Ownership) throw new Exception("Tile ownership mismatch");
        if (loaded.Meta.SchemaVersion != 1) throw new Exception("Schema version mismatch");

        File.Delete(savePath);
    }
}
