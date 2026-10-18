using Godot;
using LandBuilder.Domain;

namespace LandBuilder.Presentation;

/// <summary>
/// A finished wonder spills over into the tiles around it, so it reads as the heart of a district rather than a
/// model on its own: redwoods spread into neighbouring forest, banner towers rise in nearby towns, boats crowd the
/// waters by a harbour, dishes sprout on the plains around a rocket pad.
/// </summary>
public sealed partial class PropBuilder
{
    private void DressNearWonder(System.Func<Terrain, int> count)
    {
        if (_context.WonderNear is not WonderType wonder)
            return;

        void Some(Terrain terrain, int min, int tries, PropFactory factory, float footprint)
        {
            if (count(terrain) < min)
                return;
            for (var i = 0; i < tries; i++)
                TryLandmark(terrain, factory, footprint);
        }

        switch (wonder)
        {
            case WonderType.SequoiaPark:
                Some(Terrain.Forest, 1, 3, AddRedwood, 0.07f);
                Some(Terrain.Grass, 2, 1, AddRedwood, 0.07f);
                break;
            case WonderType.Castle:
            case WonderType.Cathedral:
                Some(Terrain.Village, 1, 2, AddBannerTower, 0.07f);
                Some(Terrain.Mountain, 1, 1, AddBannerTower, 0.07f);
                break;
            case WonderType.GreatPagoda:
                Some(Terrain.Forest, 1, 2, (p, s) => AddTree(TreeSpecies.Cherry, p, s * 1.2f), 0.07f);
                Some(Terrain.Village, 1, 2, AddStoneLantern, 0.04f);
                Some(Terrain.Grass, 1, 1, (p, s) => AddTree(TreeSpecies.Cherry, p, s * 1.2f), 0.07f);
                break;
            case WonderType.Harbour:
            case WonderType.TallShip:
            case WonderType.Lighthouse:
                Some(Terrain.Water, 2, 2, AddBoat, 0.13f);
                Some(Terrain.Village, 1, 1, AddBannerTower, 0.07f);
                break;
            case WonderType.RocketPad:
                Some(Terrain.Field, 1, 1, AddDish, 0.06f);
                Some(Terrain.Grass, 1, 2, AddDish, 0.06f);
                break;
            case WonderType.Observatory:
                Some(Terrain.Mountain, 1, 1, AddDish, 0.06f);
                Some(Terrain.Snow, 1, 1, AddDish, 0.06f);
                Some(Terrain.Forest, 1, 1, AddDish, 0.06f);
                break;
            case WonderType.BotanicalPark:
                Some(Terrain.Grass, 1, 3, AddFlowerBed, 0.05f);
                Some(Terrain.Forest, 1, 1, (p, s) => AddTree(TreeSpecies.Palm, p, s), 0.06f);
                break;
        }
    }

    /// <summary>A coast redwood towering over the ordinary forest.</summary>
    private void AddRedwood(Vector3 p, float scale)
    {
        var bark = new Color("9a4b2c");
        var leaf = new Color("2f5a37");
        _kit.Sway = Sway.Trunk;
        At(p + new Vector3(0, -0.01f, 0), _rng.Randf() * Mathf.Tau, scale);
        _kit.Frustum(7, 0.03f, 0.018f, 0.36f, bark, bark.Darkened(0.1f));
        _kit.Sway = Sway.Evergreen;
        for (var i = 0; i < 4; i++)
        {
            At(p + new Vector3(0, (0.2f + (i * 0.07f)) * scale, 0), i, scale);
            _kit.Frustum(7, 0.075f - (i * 0.014f), 0.012f, 0.12f, i % 2 == 0 ? leaf : leaf.Lightened(0.08f));
        }
    }

    /// <summary>A slim stone tower flying the town's colours.</summary>
    private void AddBannerTower(Vector3 p, float scale)
    {
        _kit.Sway = Sway.Building;
        var stone = Palette.Stone;
        At(p + new Vector3(0, -0.01f, 0), 0f, scale * BuildingScale);
        _kit.Frustum(8, 0.035f, 0.03f, 0.2f, stone, stone.Lightened(0.05f));
        _kit.Sway = Sway.Roof;
        At(p + new Vector3(0, 0.19f * scale * BuildingScale, 0), 0f, scale * BuildingScale);
        _kit.Frustum(8, 0.045f, 0f, 0.07f, new Color("4a5a6e"));
        _kit.Sway = Sway.Grass;
        At(p + new Vector3(0.02f, 0.24f * scale * BuildingScale, 0), 0f, scale * BuildingScale);
        _kit.Box(new Vector3(0.04f, 0.022f, 0.003f), new Color("c4452f"));
    }

    private void AddDish(Vector3 p, float scale)
    {
        _kit.Sway = Sway.Rigid;
        At(p, 0f, scale);
        _kit.Box(new Vector3(0.01f, 0.08f, 0.01f), new Color("c9c4bb"));
        At(p + new Vector3(0, 0.08f * scale, 0), new Basis(Vector3.Forward, 0.6f).Scaled(Vector3.One * scale));
        _kit.Frustum(12, 0.006f, 0.05f, 0.02f, new Color("f4f1ea"));
    }

    private void AddFlowerBed(Vector3 p, float scale)
    {
        _kit.Sway = Sway.Flower;
        Color[] beds = [new("e86f8a"), new("f2c94c"), new("9b7ede"), new("ff9671")];
        At(p, _rng.Randf() * Mathf.Tau, scale);
        _kit.Blob(new Vector3(0.05f, 0.018f, 0.035f), Pick(beds), _rng.RandiRange(0, 99), 0.2f);
    }
}
