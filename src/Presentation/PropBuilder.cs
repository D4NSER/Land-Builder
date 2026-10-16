using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using LandBuilder.Domain;

namespace LandBuilder.Presentation;

public enum AnimatedPartKind
{
    WindmillBlades,
    Boat,
    Smoke
}

public sealed record AnimatedPart(AnimatedPartKind Kind, Node3D Node, float Phase, Vector3 BasePosition);

/// <summary>
/// Scatters low-poly decorations over each sector (trees, houses, hay, rocks, boats...). Everything static is merged
/// into one mesh per tile; CUSTOM0 stores each prop's pivot and pop-in delay so the shader can grow them in.
/// </summary>
public sealed class PropBuilder
{
    private readonly TerrainSampler _sampler;
    private readonly TileSpec _tile;
    private readonly int _seed;
    private readonly MeshKit _kit = new();
    private readonly List<Vector3> _occupied = new();
    private readonly List<AnimatedPart> _parts = new();
    private readonly Node3D _partsRoot;
    private RandomNumberGenerator _rng = new();

    public PropBuilder(TerrainSampler sampler, TileSpec tile, int seed, Node3D partsRoot)
    {
        _sampler = sampler;
        _tile = tile;
        _seed = seed;
        _partsRoot = partsRoot;
    }

    public IReadOnlyList<AnimatedPart> Parts => _parts;

    public ArrayMesh? Build()
    {
        _rng.Seed = (ulong)(uint)_seed * 2654435761UL + 17UL;
        var counts = _tile.ToArray().GroupBy(t => t).ToDictionary(g => g.Key, g => g.Count());

        // Landmarks first so they claim the best spot.
        if (counts.GetValueOrDefault(Terrain.Village) >= 3 && _rng.Randf() < 0.7f)
            TryLandmark(Terrain.Village, AddChurch, 0.2f);
        if (counts.GetValueOrDefault(Terrain.Field) >= 3 && _rng.Randf() < 0.6f)
            TryLandmark(Terrain.Field, AddWindmill, 0.18f);
        if (counts.GetValueOrDefault(Terrain.Water) >= 4 && _rng.Randf() < 0.45f)
            TryLandmark(Terrain.Water, AddBoat, 0.15f);

        for (var sector = 0; sector < 6; sector++)
        {
            var terrain = _sampler.SectorTerrain(sector);
            switch (terrain)
            {
                case Terrain.Forest:
                    Scatter(sector, terrain, _rng.RandiRange(7, 10), 0.085f, AddForestTree);
                    break;
                case Terrain.Grass:
                    Scatter(sector, terrain, _rng.RandiRange(3, 6), 0.06f, AddMeadowDetail);
                    break;
                case Terrain.Field:
                    Scatter(sector, terrain, _rng.RandiRange(1, 3), 0.1f, AddFieldDetail);
                    break;
                case Terrain.Village:
                    Scatter(sector, terrain, _rng.RandiRange(3, 4), 0.13f, AddHouse);
                    Scatter(sector, terrain, _rng.RandiRange(1, 2), 0.06f, AddVillageGreen);
                    break;
                case Terrain.Water:
                    Scatter(sector, terrain, _rng.RandiRange(1, 3), 0.07f, AddWaterDetail);
                    break;
                case Terrain.Mountain:
                    Scatter(sector, terrain, _rng.RandiRange(3, 5), 0.08f, AddMountainDetail, edgeMargin: 0.04f);
                    break;
                case Terrain.Sand:
                    Scatter(sector, terrain, _rng.RandiRange(2, 3), 0.1f, AddDesertDetail);
                    break;
            }
        }

        return _kit.IsEmpty ? null : _kit.Commit(withCustom: true);
    }

    // ---------------------------------------------------------------- placement helpers

    private delegate void PropFactory(Vector3 position, float scale);

    private void Scatter(int sector, Terrain terrain, int count, float spacing, PropFactory factory, float edgeMargin = 0.07f)
    {
        Span<float> w = stackalloc float[7];
        var placed = 0;
        for (var attempt = 0; attempt < count * 10 && placed < count; attempt++)
        {
            var angle = HexLayout.EdgeAngle(sector) + _rng.RandfRange(-0.5f, 0.5f);
            var radius = Mathf.Sqrt(_rng.RandfRange(0.03f, 1f)) * HexLayout.InRadius * 0.95f;
            var p = HexLayout.AngleToDirection(angle) * radius;
            if (HexLayout.DistanceToHexEdge(p.X, p.Z) < edgeMargin)
                continue;
            _sampler.TerrainWeights(p.X, p.Z, w);
            if (w[(int)terrain] < 0.88f)
                continue;
            if (_occupied.Any(o => o.DistanceTo(new Vector3(p.X, 0, p.Z)) < spacing))
                continue;

            var y = _sampler.Evaluate(p.X, p.Z).Height;
            var position = new Vector3(p.X, y, p.Z);
            _occupied.Add(new Vector3(p.X, 0, p.Z));
            Begin(position);
            factory(position, _rng.RandfRange(0.85f, 1.15f));
            placed++;
        }
    }

    private void TryLandmark(Terrain terrain, PropFactory factory, float footprint)
    {
        Span<float> w = stackalloc float[7];
        for (var attempt = 0; attempt < 40; attempt++)
        {
            var angle = _rng.Randf() * Mathf.Tau;
            var radius = _rng.RandfRange(0.0f, 0.55f);
            var p = HexLayout.AngleToDirection(angle) * radius;
            if (HexLayout.DistanceToHexEdge(p.X, p.Z) < footprint + 0.05f)
                continue;
            _sampler.TerrainWeights(p.X, p.Z, w);
            if (w[(int)terrain] < 0.97f)
                continue;
            // Check the whole footprint stays inside the terrain.
            var ok = true;
            for (var i = 0; i < 6 && ok; i++)
            {
                var q = p + (HexLayout.AngleToDirection(i * Mathf.Pi / 3f) * footprint);
                _sampler.TerrainWeights(q.X, q.Z, w);
                ok = w[(int)terrain] > 0.9f;
            }

            if (!ok)
                continue;

            var y = terrain == Terrain.Water ? TerrainSampler.WaterLevel : _sampler.Evaluate(p.X, p.Z).Height;
            var position = new Vector3(p.X, y, p.Z);
            _occupied.Add(new Vector3(p.X, 0, p.Z));
            _occupied.Add(new Vector3(p.X + footprint * 0.7f, 0, p.Z));
            _occupied.Add(new Vector3(p.X - footprint * 0.7f, 0, p.Z));
            _occupied.Add(new Vector3(p.X, 0, p.Z + footprint * 0.7f));
            _occupied.Add(new Vector3(p.X, 0, p.Z - footprint * 0.7f));
            Begin(position);
            factory(position, 1f);
            return;
        }
    }

    /// <summary>Sets the pop-in pivot and delay. Props near the centre appear first, rippling outward.</summary>
    private void Begin(Vector3 pivot)
    {
        var dist = new Vector2(pivot.X, pivot.Z).Length();
        var delay = Mathf.Clamp((dist / HexLayout.Radius * 0.75f) + _rng.RandfRange(0f, 0.25f), 0f, 1f);
        _kit.Custom = new Color(pivot.X, pivot.Y, pivot.Z, delay);
        _kit.Sway = 0f;
    }

    private void At(Vector3 position, float yaw, float scale) =>
        _kit.Transform = new Transform3D(new Basis(Vector3.Up, yaw).Scaled(Vector3.One * scale), position);

    private void At(Vector3 position, Basis basis) => _kit.Transform = new Transform3D(basis, position);

    private static Color Vary(Color c, RandomNumberGenerator rng, float amount = 0.08f) =>
        new(c.R * (1 + rng.RandfRange(-amount, amount)), c.G * (1 + rng.RandfRange(-amount, amount)), c.B * (1 + rng.RandfRange(-amount, amount)));

    // ---------------------------------------------------------------- vegetation

    private void AddForestTree(Vector3 p, float scale)
    {
        if (_rng.Randf() < 0.68f)
            AddConifer(p, scale * _rng.RandfRange(0.9f, 1.25f));
        else
            AddRoundTree(p, scale, _rng.Randf() < 0.12f ? Palette.AutumnOrange : Palette.LeafGreen);
    }

    private void AddConifer(Vector3 p, float scale)
    {
        var yaw = _rng.Randf() * Mathf.Tau;
        At(p, yaw, scale);
        _kit.Sway = 0.1f;
        _kit.Frustum(5, 0.024f, 0.018f, 0.06f, Palette.TrunkBrown);
        _kit.Sway = 1f;
        var green = Vary(_rng.Randf() < 0.5f ? Palette.PineGreen : Palette.PineGreenDark, _rng);
        float[] radii = [0.105f, 0.083f, 0.058f];
        for (var i = 0; i < radii.Length; i++)
        {
            At(p + new Vector3(0, (0.045f + (i * 0.075f)) * scale, 0), yaw + (i * 0.4f), scale);
            _kit.Frustum(7, radii[i], 0.008f, 0.13f, green.Lightened(i * 0.05f), green.Darkened(0.1f), capBottom: true, bottom: green.Darkened(0.25f));
        }
    }

    private void AddRoundTree(Vector3 p, float scale, Color leaf)
    {
        var yaw = _rng.Randf() * Mathf.Tau;
        At(p, yaw, scale);
        _kit.Sway = 0.1f;
        _kit.Frustum(5, 0.022f, 0.016f, 0.11f, Palette.TrunkBrown);
        _kit.Sway = 1f;
        At(p + new Vector3(0, 0.15f * scale, 0), yaw, scale);
        _kit.Blob(new Vector3(0.095f, 0.09f, 0.095f), Vary(leaf, _rng), _rng.RandiRange(0, 9999));
        if (_rng.Randf() < 0.5f)
        {
            At(p + new Vector3(0.04f, 0.2f, 0.02f) * scale, yaw, scale);
            _kit.Blob(new Vector3(0.06f, 0.055f, 0.06f), Vary(leaf.Lightened(0.08f), _rng), _rng.RandiRange(0, 9999));
        }
    }

    private void AddBush(Vector3 p, float scale, Color color)
    {
        At(p, _rng.Randf() * Mathf.Tau, scale);
        _kit.Sway = 0.5f;
        _kit.Blob(new Vector3(0.05f, 0.035f, 0.045f), Vary(color, _rng), _rng.RandiRange(0, 9999));
    }

    private void AddFlowers(Vector3 p, float scale)
    {
        Color[] colors = [new("f6f1e3"), new("f2c94c"), new("e86f8a"), new("9b7ede"), new("ffffff")];
        var c = colors[_rng.RandiRange(0, colors.Length - 1)];
        var n = _rng.RandiRange(4, 7);
        _kit.Sway = 0.7f;
        for (var i = 0; i < n; i++)
        {
            var offset = new Vector3(_rng.RandfRange(-0.05f, 0.05f), 0, _rng.RandfRange(-0.05f, 0.05f));
            var q = p + offset;
            q.Y = _sampler.Evaluate(q.X, q.Z).Height;
            At(q, _rng.Randf() * Mathf.Tau, scale);
            _kit.Frustum(3, 0.004f, 0.003f, 0.03f, Palette.LeafGreen);
            At(q + new Vector3(0, 0.03f * scale, 0), _rng.Randf(), scale);
            _kit.Blob(new Vector3(0.012f, 0.008f, 0.012f), c, i, 0.05f);
        }
    }

    private void AddRock(Vector3 p, float scale, Color color)
    {
        At(p + new Vector3(0, -0.01f, 0), _rng.Randf() * Mathf.Tau, scale);
        _kit.Blob(new Vector3(0.045f, 0.03f, 0.038f) * _rng.RandfRange(0.7f, 1.4f), Vary(color, _rng, 0.05f), _rng.RandiRange(0, 9999), 0.25f);
    }

    private void AddMeadowDetail(Vector3 p, float scale)
    {
        var roll = _rng.Randf();
        if (roll < 0.45f)
            AddFlowers(p, scale);
        else if (roll < 0.7f)
            AddBush(p, scale, Palette.LeafGreenLight);
        else if (roll < 0.85f)
            AddRoundTree(p, scale * 0.9f, Palette.LeafGreenLight);
        else
            AddRock(p, scale * 0.7f, Palette.Stone);
    }

    // ---------------------------------------------------------------- farmland

    private void AddFieldDetail(Vector3 p, float scale)
    {
        var roll = _rng.Randf();
        if (roll < 0.6f)
        {
            // Hay bale lying on its side.
            var yaw = _rng.Randf() * Mathf.Tau;
            var basis = new Basis(Vector3.Up, yaw) * new Basis(Vector3.Forward, Mathf.Pi / 2f);
            At(p + new Vector3(0, 0.032f, 0) + (new Basis(Vector3.Up, yaw) * new Vector3(0, 0, 0)), basis.Scaled(Vector3.One * scale));
            _kit.Transform = new Transform3D(basis.Scaled(Vector3.One * scale), p + new Vector3(0, 0.035f * scale, 0) + (new Basis(Vector3.Up, yaw) * new Vector3(-0.035f * scale, 0, 0)));
            _kit.Frustum(8, 0.035f, 0.035f, 0.07f, Palette.Hay, Palette.Hay.Darkened(0.1f), capBottom: true, bottom: Palette.Hay.Darkened(0.1f));
        }
        else if (roll < 0.85f)
        {
            // Little wheat sheaves.
            _kit.Sway = 0.8f;
            for (var i = 0; i < 3; i++)
            {
                var q = p + new Vector3(_rng.RandfRange(-0.04f, 0.04f), 0, _rng.RandfRange(-0.04f, 0.04f));
                At(q, _rng.Randf() * Mathf.Tau, scale);
                _kit.Cone(5, 0.018f, 0.075f, Palette.Wheat.Darkened(_rng.RandfRange(0f, 0.12f)));
            }
        }
        else
        {
            AddScarecrow(p, scale);
        }
    }

    private void AddScarecrow(Vector3 p, float scale)
    {
        var yaw = _rng.Randf() * Mathf.Tau;
        At(p, yaw, scale);
        _kit.Box(new Vector3(0.012f, 0.12f, 0.012f), Palette.TrunkBrown);
        At(p + new Vector3(0, 0.08f * scale, 0), yaw, scale);
        _kit.Box(new Vector3(0.09f, 0.012f, 0.012f), Palette.TrunkBrown);
        At(p + new Vector3(0, 0.06f * scale, 0), yaw, scale);
        _kit.Box(new Vector3(0.035f, 0.045f, 0.02f), new Color("c25a3a"));
        At(p + new Vector3(0, 0.12f * scale, 0), yaw, scale);
        _kit.Cone(6, 0.028f, 0.03f, Palette.Hay);
    }

    private void AddWindmill(Vector3 p, float scale)
    {
        var yaw = _rng.Randf() * Mathf.Tau;
        At(p, yaw, scale);
        _kit.Frustum(8, 0.075f, 0.05f, 0.32f, Palette.Wall, Palette.Wall.Darkened(0.1f));
        At(p + new Vector3(0, 0.32f, 0), yaw, scale);
        _kit.Cone(8, 0.065f, 0.08f, Palette.RoofRed);
        At(p + new Vector3(0, 0.0f, 0), yaw, scale);
        _kit.Box(new Vector3(0.04f, 0.07f, 0.002f), Palette.TrunkBrown.Darkened(0.2f));

        // Blades spin as a separate node.
        var hub = new Vector3(0, 0.27f, 0.065f);
        var bladesKit = new MeshKit { Custom = new Color(0, 0, 0, 0) };
        for (var i = 0; i < 4; i++)
        {
            bladesKit.Transform = new Transform3D(new Basis(Vector3.Back, i * Mathf.Pi / 2f), Vector3.Zero);
            bladesKit.Box(new Vector3(0.012f, 0.2f, 0.01f), Palette.TrunkBrown);
            bladesKit.Transform = new Transform3D(new Basis(Vector3.Back, i * Mathf.Pi / 2f), new Vector3(0.022f, 0.06f, 0.004f));
            bladesKit.Box(new Vector3(0.035f, 0.14f, 0.004f), Palette.Wall.Darkened(0.05f));
        }

        var node = new Node3D { Name = "WindmillBlades" };
        var mesh = new MeshInstance3D { Mesh = bladesKit.Commit(withCustom: true), CastShadow = GeometryInstance3D.ShadowCastingSetting.On };
        node.AddChild(mesh);
        node.Transform = new Transform3D(new Basis(Vector3.Up, yaw).Scaled(Vector3.One * scale), p + (new Basis(Vector3.Up, yaw) * hub * scale));
        _partsRoot.AddChild(node);
        _parts.Add(new AnimatedPart(AnimatedPartKind.WindmillBlades, node, _rng.Randf() * Mathf.Tau, node.Position));
    }

    // ---------------------------------------------------------------- village

    private void AddHouse(Vector3 p, float scale)
    {
        // Face roughly towards the tile centre with a little jitter, like streets radiating out.
        var toCentre = Mathf.Atan2(p.X, p.Z);
        var yaw = toCentre + (Mathf.Round(_rng.RandfRange(-1f, 1f)) * Mathf.Pi / 2f) + _rng.RandfRange(-0.15f, 0.15f);
        var w = _rng.RandfRange(0.11f, 0.15f);
        var d = _rng.RandfRange(0.085f, 0.105f);
        var h = _rng.RandfRange(0.07f, 0.1f);
        Color[] roofs = [Palette.RoofRed, Palette.RoofRed, Palette.RoofBlue, Palette.RoofOrange, new("8b5a3c")];
        var roof = Vary(roofs[_rng.RandiRange(0, roofs.Length - 1)], _rng, 0.06f);
        var wall = _rng.Randf() < 0.7f ? Palette.Wall : Palette.WallWarm;

        At(p + new Vector3(0, -0.01f, 0), yaw, scale);
        _kit.Box(new Vector3(w, h + 0.01f, d), wall);
        At(p + new Vector3(0, h, 0), yaw, scale);
        _kit.GableRoof(w * 1.12f, d * 1.22f, 0.065f, roof, wall.Darkened(0.06f));
        if (_rng.Randf() < 0.6f)
        {
            var chimney = new Basis(Vector3.Up, yaw) * new Vector3(w * 0.28f, h + 0.03f, d * 0.18f) * scale;
            At(p + chimney, yaw, scale);
            _kit.Box(new Vector3(0.018f, 0.05f, 0.018f), Palette.StoneDark);
        }

        // Door
        At(p + (new Basis(Vector3.Up, yaw) * new Vector3(0, 0, d * 0.5f + 0.001f) * scale), yaw, scale);
        _kit.Box(new Vector3(0.022f, 0.04f, 0.003f), new Color("6b4a32"));
    }

    private void AddChurch(Vector3 p, float scale)
    {
        var yaw = _rng.Randf() * Mathf.Tau;
        At(p + new Vector3(0, -0.01f, 0), yaw, scale);
        _kit.Box(new Vector3(0.22f, 0.13f, 0.13f), Palette.Wall);
        At(p + new Vector3(0, 0.12f, 0), yaw, scale);
        _kit.GableRoof(0.24f, 0.16f, 0.08f, Palette.RoofBlue, Palette.Wall.Darkened(0.05f));
        var towerOffset = new Basis(Vector3.Up, yaw) * new Vector3(-0.13f, 0, 0) * scale;
        At(p + towerOffset + new Vector3(0, -0.01f, 0), yaw, scale);
        _kit.Box(new Vector3(0.08f, 0.27f, 0.08f), Palette.Wall);
        At(p + towerOffset + new Vector3(0, 0.26f * scale, 0), yaw + (Mathf.Pi / 4f), scale);
        _kit.Frustum(4, 0.065f, 0f, 0.16f, Palette.RoofBlue);
        At(p + towerOffset + (new Basis(Vector3.Up, yaw) * new Vector3(0, 0.19f, 0.041f) * scale), yaw, scale);
        _kit.Box(new Vector3(0.03f, 0.04f, 0.003f), Palette.Gold);
    }

    private void AddVillageGreen(Vector3 p, float scale)
    {
        if (_rng.Randf() < 0.6f)
            AddRoundTree(p, scale * 0.85f, Palette.LeafGreen);
        else
            AddBush(p, scale, Palette.LeafGreen);
    }

    // ---------------------------------------------------------------- water

    private void AddWaterDetail(Vector3 p, float scale)
    {
        var roll = _rng.Randf();
        var surface = new Vector3(p.X, TerrainSampler.WaterLevel + 0.003f, p.Z);
        if (roll < 0.55f)
        {
            var n = _rng.RandiRange(2, 4);
            for (var i = 0; i < n; i++)
            {
                var q = surface + new Vector3(_rng.RandfRange(-0.05f, 0.05f), 0, _rng.RandfRange(-0.05f, 0.05f));
                At(q, _rng.Randf() * Mathf.Tau, scale * _rng.RandfRange(0.6f, 1.1f));
                _kit.Frustum(7, 0.03f, 0.03f, 0.004f, Palette.Lily, Palette.Lily.Lightened(0.08f));
                if (_rng.Randf() < 0.3f)
                {
                    At(q + new Vector3(0.008f, 0.004f, 0), 0, scale);
                    _kit.Blob(new Vector3(0.008f, 0.006f, 0.008f), new Color("f7d6e0"), i);
                }
            }
        }
        else
        {
            AddRock(surface + new Vector3(0, -0.01f, 0), scale * 0.9f, Palette.StoneDark);
        }
    }

    private void AddBoat(Vector3 p, float scale)
    {
        var kit = new MeshKit();
        var hull = new Color("8a5a3b");
        kit.Transform = Transform3D.Identity;
        kit.Box(new Vector3(0.18f, 0.035f, 0.07f), hull, hull.Lightened(0.15f));
        kit.Transform = new Transform3D(Basis.Identity, new Vector3(0.09f, 0, 0));
        kit.Frustum(3, 0.035f, 0.0f, 0.035f, hull);
        kit.Transform = new Transform3D(Basis.Identity, new Vector3(0, 0.03f, 0));
        kit.Box(new Vector3(0.008f, 0.17f, 0.008f), Palette.TrunkBrown);
        kit.Transform = new Transform3D(Basis.Identity, new Vector3(0.0f, 0.06f, 0f));
        kit.OrientFrom = null;
        var sail = Palette.Wall;
        kit.Triangle(new Vector3(0.005f, 0.0f, 0), new Vector3(0.09f, 0.0f, 0), new Vector3(0.005f, 0.13f, 0), sail);
        kit.Triangle(new Vector3(0.005f, 0.0f, 0), new Vector3(0.005f, 0.13f, 0), new Vector3(0.09f, 0.0f, 0), sail);

        var node = new Node3D { Name = "Boat" };
        node.AddChild(new MeshInstance3D { Mesh = kit.Commit(withCustom: true) });
        node.Position = p + new Vector3(0, -0.012f, 0);
        node.Rotation = new Vector3(0, _rng.Randf() * Mathf.Tau, 0);
        _partsRoot.AddChild(node);
        _parts.Add(new AnimatedPart(AnimatedPartKind.Boat, node, _rng.Randf() * Mathf.Tau, node.Position));
    }

    // ---------------------------------------------------------------- mountains & dunes

    private void AddMountainDetail(Vector3 p, float scale)
    {
        if (p.Y < 0.3f && _rng.Randf() < 0.75f)
            AddConifer(p, scale * 0.8f);
        else if (p.Y < 0.55f)
            AddRock(p, scale * 1.2f, Palette.StoneDark);
    }

    private void AddDesertDetail(Vector3 p, float scale)
    {
        Span<float> w = stackalloc float[7];
        // Palms near water, cacti inland.
        var nearWater = false;
        for (var i = 0; i < 6 && !nearWater; i++)
        {
            var q = p + (HexLayout.AngleToDirection(i * Mathf.Pi / 3f) * 0.22f);
            _sampler.TerrainWeights(q.X, q.Z, w);
            nearWater = w[(int)Terrain.Water] > 0.3f;
        }

        var roll = _rng.Randf();
        if (nearWater && roll < 0.75f)
            AddPalm(p, scale);
        else if (roll < 0.6f)
            AddCactus(p, scale);
        else if (roll < 0.85f)
            AddBush(p, scale * 0.8f, new Color("a8a35a"));
        else
            AddRock(p, scale, new Color("c9a77a"));
    }

    private void AddCactus(Vector3 p, float scale)
    {
        var yaw = _rng.Randf() * Mathf.Tau;
        _kit.Sway = 0.05f;
        At(p, yaw, scale);
        _kit.Frustum(6, 0.022f, 0.02f, 0.16f, Palette.Cactus, Palette.Cactus.Lightened(0.1f));
        var basis = new Basis(Vector3.Up, yaw);
        At(p + (basis * new Vector3(0.025f, 0.06f, 0) * scale), yaw, scale);
        _kit.Frustum(5, 0.012f, 0.012f, 0.06f, Palette.Cactus);
        At(p + (basis * new Vector3(-0.025f, 0.09f, 0) * scale), yaw, scale);
        _kit.Frustum(5, 0.011f, 0.011f, 0.045f, Palette.Cactus);
    }

    private void AddPalm(Vector3 p, float scale)
    {
        var yaw = _rng.Randf() * Mathf.Tau;
        var lean = _rng.RandfRange(0.1f, 0.3f);
        var basis = new Basis(Vector3.Up, yaw) * new Basis(Vector3.Forward, lean);
        _kit.Sway = 0.3f;
        At(p, basis.Scaled(Vector3.One * scale));
        _kit.Frustum(5, 0.016f, 0.011f, 0.24f, new Color("9c7650"));
        var top = p + (basis * new Vector3(0, 0.24f, 0) * scale);
        _kit.Sway = 1f;
        for (var i = 0; i < 6; i++)
        {
            var leafYaw = (i * Mathf.Tau / 6f) + _rng.RandfRange(-0.2f, 0.2f);
            var leafBasis = new Basis(Vector3.Up, leafYaw) * new Basis(Vector3.Right, -0.35f);
            _kit.Transform = new Transform3D(leafBasis.Scaled(Vector3.One * scale), top);
            _kit.OrientFrom = null;
            var c = Vary(Palette.LeafGreen, _rng);
            _kit.Triangle(new Vector3(-0.02f, 0, 0), new Vector3(0.02f, 0, 0), new Vector3(0, -0.04f, 0.13f), c);
            _kit.Triangle(new Vector3(0.02f, 0, 0), new Vector3(-0.02f, 0, 0), new Vector3(0, -0.04f, 0.13f), c.Darkened(0.15f));
        }

        _kit.Transform = new Transform3D(Basis.Identity, top);
        _kit.Blob(new Vector3(0.02f, 0.018f, 0.02f), new Color("6b4a32"), 3);
    }
}
