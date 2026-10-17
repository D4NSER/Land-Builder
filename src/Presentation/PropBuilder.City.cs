using LandBuilder.Domain;
using Godot;

namespace LandBuilder.Presentation;

/// <summary>
/// Settlements grow with their region's rung: a Village gets a market, a Town taller houses and lamp-lit streets,
/// a City apartment blocks, a Metropolis glass towers, and a Capital one great spire that dominates the skyline.
/// Meadows get grazing animals and fences so open country never looks bare.
/// </summary>
public sealed partial class PropBuilder
{
    private const int CityTier = 3;
    private const int MetropolisTier = 4;
    private const int CapitalTier = 5;

    /// <summary>Downtown landmarks claimed before the houses scatter around them.</summary>
    private void BuildSkyline(int villageSectors)
    {
        var tier = _context.VillageTier;
        if (tier < CityTier || villageSectors < 2)
            return;

        // One spire at the very heart of a Capital; glass towers cluster downtown and thin out towards the edge.
        var downtown = _context.Downtown;
        if (tier >= CapitalTier && downtown >= 1f && villageSectors >= 3)
            TryLandmark(Terrain.Village, (p, _) => AddSpireTower(p), 0.14f);

        var towers = tier >= MetropolisTier ? Mathf.RoundToInt(downtown * (villageSectors >= 4 ? 3.2f : 2f)) : 0;
        for (var i = 0; i < towers; i++)
            TryLandmark(Terrain.Village, (p, s) => AddGlassTower(p, s * (0.75f + (downtown * 0.35f))), 0.09f);

        var blocks = Mathf.RoundToInt((0.6f + downtown) * (villageSectors >= 4 ? 2f : 1.2f));
        for (var i = 0; i < blocks; i++)
            TryLandmark(Terrain.Village, (p, s) => AddApartmentBlock(p, s), 0.1f);
    }

    /// <summary>In cities, most homes are replaced by terraces and small blocks.</summary>
    private void AddCityHouse(Vector3 p, float scale)
    {
        var tier = _context.VillageTier;
        var urban = (tier >= CapitalTier ? 0.45f : tier >= MetropolisTier ? 0.35f : tier >= CityTier ? 0.25f : 0f) * (0.5f + _context.Downtown);
        if (_rng.Randf() < urban)
            AddApartmentBlock(p, scale * 0.8f);
        else
            AddHouse(p, scale * BuildingScale);
    }

    private Color CityWall() => _style.Architecture switch
    {
        Architecture.Japanese => Pick(new[] { new Color("d9d6cf"), new Color("c8c2b8"), new Color("e6e1d6") }),
        Architecture.Nordic => Pick(new[] { new Color("8a4b3a"), new Color("d8c9a8"), new Color("5d6b74") }),
        Architecture.Tropical => Pick(new[] { new Color("f6f0e2"), new Color("f2d8b4"), new Color("cfe6e0") }),
        Architecture.African => Pick(new[] { new Color("d9a066"), new Color("e8c89a"), new Color("c88752") }),
        _ => Vary(Pick(CityPastels), _rng, 0.04f)
    };

    private static readonly Color[] CityPastels =
        [new("efe0c4"), new("d9b99b"), new("c9d6df"), new("e6c3a5"), new("f2e6d4"), new("b9c7b0"), new("d8a48f"), new("e9d38f")];

    /// <summary>A three- to six-storey block with rows of windows, a cornice and a roof garden or water tank.</summary>
    private void AddApartmentBlock(Vector3 p, float scale)
    {
        _kit.Sway = Sway.Building;
        var yaw = HouseYaw(p);
        var storeys = _rng.RandiRange(3, 3 + _context.VillageTier - 1);
        var w = _rng.RandfRange(0.12f, 0.16f);
        var d = _rng.RandfRange(0.1f, 0.13f);
        const float storey = 0.055f;
        var h = storeys * storey;
        var wall = CityWall();
        var glass = new Color("3d5568");
        At(p + new Vector3(0, -0.01f, 0), yaw, scale);
        _kit.Box(new Vector3(w, h + 0.01f, d), wall, wall.Lightened(0.05f));

        // Window bands on the long faces, a darker plinth and a cornice.
        for (var level = 0; level < storeys; level++)
        {
            var y = (level * storey) + (storey * 0.35f);
            foreach (var side in new[] { -1f, 1f })
            {
                At(Local(p, yaw, new Vector3(0, y, side * ((d / 2f) + 0.001f)), scale), yaw, scale);
                _kit.Box(new Vector3(w * 0.84f, storey * 0.38f, 0.004f), level == 0 ? wall.Darkened(0.3f) : glass);
            }
        }

        At(p + new Vector3(0, h, 0), yaw, scale);
        _kit.Box(new Vector3(w + 0.012f, 0.012f, d + 0.012f), wall.Darkened(0.12f));
        if (_rng.Randf() < 0.5f)
        {
            At(Local(p, yaw, new Vector3(w * 0.22f, h + 0.012f, 0), scale), yaw, scale);
            _kit.Frustum(8, 0.018f, 0.018f, 0.035f, new Color("8b6a4a"));
            At(Local(p, yaw, new Vector3(w * 0.22f, h + 0.047f, 0), scale), yaw, scale);
            _kit.Frustum(8, 0.02f, 0f, 0.015f, new Color("5a4636"));
        }
        else
        {
            _kit.Sway = Sway.Bush;
            At(p + new Vector3(0, h + 0.012f, 0), yaw, scale);
            _kit.Blob(new Vector3(w * 0.35f, 0.012f, d * 0.3f), Pick(_style.Leaves), _rng.RandiRange(0, 99), 0.15f);
        }
    }

    /// <summary>A glass tower: stone podium, banded shaft, setback crown and an antenna.</summary>
    private void AddGlassTower(Vector3 p, float scale)
    {
        _kit.Sway = Sway.Building;
        var yaw = HouseYaw(p);
        var height = _rng.RandfRange(0.38f, 0.62f) * (_context.VillageTier >= CapitalTier ? 1.15f : 1f);
        var w = _rng.RandfRange(0.085f, 0.11f);
        var glass = Pick(new[] { new Color("7fa7c4"), new Color("8bb8c9"), new Color("6f93ad"), new Color("a6c3cf") });
        var podium = CityWall();
        At(p + new Vector3(0, -0.01f, 0), yaw, scale);
        _kit.Box(new Vector3(w * 1.5f, 0.06f, w * 1.5f), podium);

        var bands = Mathf.RoundToInt(height / 0.045f);
        for (var i = 0; i < bands; i++)
        {
            var y = 0.05f + (i * height / bands);
            At(p + new Vector3(0, y, 0), yaw, scale);
            _kit.Box(new Vector3(w, height / bands, w), i % 2 == 0 ? glass : glass.Lightened(0.12f));
        }

        var top = 0.05f + height;
        At(p + new Vector3(0, top, 0), yaw, scale);
        _kit.Box(new Vector3(w * 0.7f, 0.05f, w * 0.7f), glass.Darkened(0.15f));
        At(p + new Vector3(0, top + 0.05f, 0), yaw, scale);
        _kit.Box(new Vector3(0.005f, 0.08f, 0.005f), new Color("c9c4bb"));
        At(p + new Vector3(0, top + 0.13f, 0), yaw, scale);
        _kit.Box(new Vector3(0.01f, 0.01f, 0.01f), new Color("e8553f"));
    }

    /// <summary>The Capital's signature tower: tapering tiers, a gilded crown and a needle spire.</summary>
    private void AddSpireTower(Vector3 p)
    {
        _kit.Sway = Sway.Building;
        var stone = _style.Architecture == Architecture.Japanese ? new Color("e6e1d6") : new Color("efe6d2");
        var glass = new Color("86aac2");
        var gold = new Color("e2b64a");
        At(p + new Vector3(0, -0.01f, 0), 0.2f, 1f);
        _kit.Frustum(8, 0.16f, 0.15f, 0.07f, stone, stone.Lightened(0.05f), twist: Mathf.Pi / 8f);
        float[] radii = [0.105f, 0.085f, 0.066f, 0.048f];
        float[] heights = [0.32f, 0.26f, 0.2f, 0.14f];
        var y = 0.06f;
        for (var i = 0; i < radii.Length; i++)
        {
            At(p + new Vector3(0, y, 0), 0.2f, 1f);
            _kit.Frustum(8, radii[i], radii[i] * 0.92f, heights[i], i % 2 == 0 ? glass : stone, twist: Mathf.Pi / 8f);
            y += heights[i];
            At(p + new Vector3(0, y, 0), 0.2f, 1f);
            _kit.Frustum(8, radii[i] * 1.05f, radii[i] * 1.05f, 0.012f, gold, twist: Mathf.Pi / 8f);
            y += 0.012f;
        }

        At(p + new Vector3(0, y, 0), 0.2f, 1f);
        _kit.Frustum(8, 0.035f, 0.004f, 0.32f, gold, twist: Mathf.Pi / 8f);
    }

    // ---------------------------------------------------------------- countryside

    /// <summary>Grazing animals in little groups, sometimes behind a bit of fence.</summary>
    private void AddLivestock(Vector3 p, float scale)
    {
        var cows = _style.Architecture is Architecture.Rustic or Architecture.European or Architecture.Nordic && _rng.Randf() < 0.4f;
        var count = _rng.RandiRange(2, 4);
        for (var i = 0; i < count; i++)
        {
            var offset = new Vector3(_rng.RandfRange(-0.07f, 0.07f), 0, _rng.RandfRange(-0.07f, 0.07f));
            var at = p + offset;
            if (!IsDry(at.X, at.Z) || InZone(at.X, at.Z))
                continue;
            at.Y = _sampler.Evaluate(at.X, at.Z).Height;
            var yaw = _rng.Randf() * Mathf.Tau;
            if (cows)
                AddCow(at, yaw, scale);
            else
                AddSheep(at, yaw, scale);
        }

        if (_rng.Randf() < 0.45f)
            AddFence(p + new Vector3(0.09f, 0, -0.05f), _rng.Randf() * Mathf.Tau, scale);
    }

    private void AddSheep(Vector3 p, float yaw, float scale)
    {
        _kit.Sway = Sway.Rigid;
        var wool = new Color("f3f0e8");
        var face = new Color("3a3330");
        foreach (var (x, z) in new[] { (-0.01f, -0.008f), (-0.01f, 0.008f), (0.01f, -0.008f), (0.01f, 0.008f) })
        {
            At(Local(p, yaw, new Vector3(x, 0, z), scale), yaw, scale);
            _kit.Box(new Vector3(0.004f, 0.014f, 0.004f), face);
        }

        At(p + new Vector3(0, 0.022f * scale, 0), yaw, scale);
        _kit.Blob(new Vector3(0.022f, 0.015f, 0.016f), wool, _rng.RandiRange(0, 99), 0.1f);
        At(Local(p, yaw, new Vector3(0.024f, 0.026f, 0), scale), yaw, scale);
        _kit.Box(new Vector3(0.012f, 0.012f, 0.01f), face);
    }

    private void AddCow(Vector3 p, float yaw, float scale)
    {
        _kit.Sway = Sway.Rigid;
        var hide = _rng.Randf() < 0.5f ? new Color("f2efe8") : new Color("8a5a3b");
        var dark = new Color("2e2a28");
        foreach (var (x, z) in new[] { (-0.016f, -0.009f), (-0.016f, 0.009f), (0.016f, -0.009f), (0.016f, 0.009f) })
        {
            At(Local(p, yaw, new Vector3(x, 0, z), scale), yaw, scale);
            _kit.Box(new Vector3(0.005f, 0.018f, 0.005f), dark);
        }

        At(p + new Vector3(0, 0.018f * scale, 0), yaw, scale);
        _kit.Box(new Vector3(0.046f, 0.022f, 0.024f), hide);
        At(Local(p, yaw, new Vector3(-0.006f, 0.04f, 0), scale), yaw, scale);
        _kit.Box(new Vector3(0.016f, 0.0015f, 0.025f), dark);
        At(Local(p, yaw, new Vector3(0.029f, 0.026f, 0), scale), yaw, scale);
        _kit.Box(new Vector3(0.014f, 0.016f, 0.014f), hide.Darkened(0.1f));
    }

    private void AddFence(Vector3 p, float yaw, float scale)
    {
        if (!IsDry(p.X, p.Z) || InZone(p.X, p.Z))
            return;
        p.Y = _sampler.Evaluate(p.X, p.Z).Height;
        _kit.Sway = Sway.Rigid;
        var wood = new Color("9a7552");
        for (var i = 0; i < 4; i++)
        {
            At(Local(p, yaw, new Vector3((i - 1.5f) * 0.03f, 0, 0), scale), yaw, scale);
            _kit.Box(new Vector3(0.005f, 0.026f, 0.005f), wood);
        }

        foreach (var y in new[] { 0.01f, 0.02f })
        {
            At(Local(p, yaw, new Vector3(0, y, 0), scale), yaw, scale);
            _kit.Box(new Vector3(0.1f, 0.004f, 0.003f), wood.Lightened(0.08f));
        }
    }
}
