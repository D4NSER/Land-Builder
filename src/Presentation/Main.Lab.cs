using System.Linq;
using Godot;
using LandBuilder.Domain;

namespace LandBuilder.Presentation;

/// <summary>
/// Debug-only visual lab (<c>-- --lab [--lab-hide=water,props,shelf] [--lab-off=ssao,dof,glow,shadows]</c>):
/// a fixed patch of tiles under a close camera, for isolating rendering artefacts.
/// </summary>
public partial class Main
{
    private void RunLab(string[] args)
    {
        _attract = false;
        _mode = Mode.GameOver;
        _menus.HideAll();
        _hud.Visible = false;
        var themeArg = args.FirstOrDefault(a => a.StartsWith("--theme="))?.Split('=', 2)[1];
        var theme = System.Enum.TryParse<WorldTheme>(themeArg, true, out var t) ? t : WorldTheme.Tropical;
        var state = GameState.CreateInitial(5, theme);
        state.Board.Clear();
        var origin = HexGrid.OriginSlotIndex;
        if (args.Contains("--lab-terrains"))
        {
            static TileSpec Split(Terrain a, Terrain b, int countA) =>
                TileSpec.FromArray(Enumerable.Range(0, 6).Select(i => i < countA ? a : b).ToArray());
            state.Board[origin] = new PlacedTile(TileSpec.Pure(Terrain.Snow), 0, 0);
            TileSpec[] ring =
            [
                Split(Terrain.Snow, Terrain.Mountain, 3),
                TileSpec.Pure(Terrain.Swamp),
                Split(Terrain.Swamp, Terrain.Water, 4),
                TileSpec.Pure(Terrain.Orchard),
                Split(Terrain.Orchard, Terrain.Village, 3),
                new TileSpec(Terrain.River, Terrain.Snow, Terrain.Snow, Terrain.River, Terrain.Snow, Terrain.Snow)
            ];
            for (var e = 0; e < 6; e++)
                state.Board[HexGrid.NeighborSlot(origin, e)] = new PlacedTile(ring[e], e, 0);
        }
        else
        {
            var water = TileSpec.Pure(Terrain.Water);
            var grass = TileSpec.Pure(Terrain.Grass);
            state.Board[origin] = new PlacedTile(water, 0, 0);
            for (var e = 0; e < 6; e++)
                state.Board[HexGrid.NeighborSlot(origin, e)] = new PlacedTile(e < 3 ? water : grass, e, 0);
            state.Board[HexGrid.Encode(new HexCoord(2, -1))] = new PlacedTile(new TileSpec(Terrain.River, Terrain.Grass, Terrain.Grass, Terrain.River, Terrain.Grass, Terrain.Grass), 0, 0);
        }
        _session = new LandBuilder.Application.GameSession(state, _sink);
        _board.ApplyTheme(ThemeStyle.For(state.Theme));
        _world.ApplyTheme(ThemeStyle.For(state.Theme));
        _board.Clear();
        foreach (var (slot, placed) in state.Board)
            _board.AddTileImmediate(slot, placed, pop: false);
        _hand?.Free();
        _hand = null;
        _board.SetCandidatesVisible(false);
        var far = args.Contains("--lab-far");
        _rig.Reframe(new Vector3(0.6f, 0, 0), far ? 7f : args.Contains("--lab-terrains") ? 1.8f : 1.0f, instant: true);
        _rig.ScreenShift = 0f;

        var hide = args.FirstOrDefault(a => a.StartsWith("--lab-hide="))?.Split('=', 2)[1] ?? "";
        var off = args.FirstOrDefault(a => a.StartsWith("--lab-off="))?.Split('=', 2)[1] ?? "";
        foreach (var tile in _board.Tiles.Values)
        {
            if (hide.Contains("water") && tile.Water is not null)
                tile.Water.Hide();
            if (hide.Contains("props") && tile.Props is not null)
                tile.Props.Hide();
            if (hide.Contains("shelf") && tile.Root.GetNodeOrNull<Node3D>("Shelf") is { } shelf)
                shelf.Hide();
        }

        _world.Environment.SsaoEnabled = !off.Contains("ssao");
        _world.Environment.GlowEnabled = !off.Contains("glow");
        _world.Sun.ShadowEnabled = !off.Contains("shadows");
        _rig.DepthOfField = !off.Contains("dof");
    }
}
