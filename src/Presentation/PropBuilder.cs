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
/// Scatters low-poly decorations over each sector (trees, houses, hay, rocks, boats...) plus a fine ground-detail
/// pass (grass tufts, pebbles, reeds). Everything static is merged into one mesh per tile; CUSTOM0 stores each
/// prop's pivot and pop-in delay so the shader can grow them in. The island's theme picks species and colours.
/// </summary>
public sealed class PropBuilder
{
    private readonly TerrainSampler _sampler;
    private readonly ThemeStyle _style;
    private readonly TileSpec _tile;
    private readonly int _seed;
    private readonly MeshKit _kit = new();
    private readonly List<Vector3> _occupied = new();
    private readonly List<AnimatedPart> _parts = new();
    private readonly Node3D _partsRoot;
    private readonly RandomNumberGenerator _rng = new();

    public PropBuilder(TerrainSampler sampler, TileSpec tile, int seed, Node3D partsRoot)
    {
        _sampler = sampler;
        _style = sampler.Style;
        _tile = tile;
        _seed = seed;
        _partsRoot = partsRoot;
    }

    public IReadOnlyList<AnimatedPart> Parts => _parts;

    public ArrayMesh? Build()
    {
        _rng.Seed = (ulong)(uint)_seed * 2654435761UL + 17UL;
        var counts = Enumerable.Range(0, 6).Select(_sampler.PropTerrain).GroupBy(t => t).ToDictionary(g => g.Key, g => g.Count());

        // Landmarks first so they claim the best spot.
        if (counts.GetValueOrDefault(Terrain.Village) >= 3 && _rng.Randf() < 0.7f)
            TryLandmark(Terrain.Village, AddChurch, 0.2f);
        else if (counts.GetValueOrDefault(Terrain.Village) >= 2 && _rng.Randf() < 0.35f)
            TryLandmark(Terrain.Village, AddWell, 0.08f);
        if (counts.GetValueOrDefault(Terrain.Field) >= 3 && _rng.Randf() < 0.6f)
            TryLandmark(Terrain.Field, AddWindmill, 0.18f);
        if (counts.GetValueOrDefault(Terrain.Water) >= 4 && _rng.Randf() < 0.45f)
            TryLandmark(Terrain.Water, AddBoat, 0.15f);
        if (counts.GetValueOrDefault(Terrain.Snow) >= 2 && _rng.Randf() < 0.25f)
            TryLandmark(Terrain.Snow, AddSnowman, 0.06f);
        if (counts.GetValueOrDefault(Terrain.Snow) >= 3 && _rng.Randf() < 0.35f)
            TryLandmark(Terrain.Snow, AddCabin, 0.12f);

        if (counts.ContainsKey(Terrain.Orchard))
            PlantOrchardRows();

        for (var sector = 0; sector < 6; sector++)
        {
            var terrain = _sampler.PropTerrain(sector);
            var bank = _sampler.SectorTerrain(sector) == Terrain.River;
            int Count(int min, int max) => bank ? Mathf.Max(1, _rng.RandiRange(min, max) / 2) : _rng.RandiRange(min, max);
            switch (terrain)
            {
                case Terrain.Forest:
                    Scatter(sector, terrain, Count(8, 11), 0.08f, AddForestTree);
                    Scatter(sector, terrain, Count(2, 4), 0.05f, AddUndergrowth);
                    break;
                case Terrain.Grass:
                    Scatter(sector, terrain, Count(3, 6), 0.06f, AddMeadowDetail);
                    break;
                case Terrain.Field:
                    Scatter(sector, terrain, Count(1, 3), 0.1f, AddFieldDetail);
                    break;
                case Terrain.Village:
                    Scatter(sector, terrain, Count(3, 4), 0.13f, AddHouse);
                    Scatter(sector, terrain, Count(1, 3), 0.06f, AddVillageGreen);
                    break;
                case Terrain.Water:
                    Scatter(sector, terrain, Count(1, 3), 0.07f, AddWaterDetail, allowWet: true);
                    break;
                case Terrain.Mountain:
                    Scatter(sector, terrain, Count(3, 5), 0.08f, AddMountainDetail, edgeMargin: 0.04f);
                    break;
                case Terrain.Sand:
                    Scatter(sector, terrain, Count(2, 3), 0.1f, AddDesertDetail);
                    break;
                case Terrain.Snow:
                    Scatter(sector, terrain, Count(3, 6), 0.08f, AddSnowDetail);
                    break;
                case Terrain.Swamp:
                    Scatter(sector, terrain, Count(7, 10), 0.05f, AddSwampDetail, allowWet: true);
                    break;
            }
        }

        AddGroundDetail();
        return _kit.IsEmpty ? null : _kit.Commit(withCustom: true);
    }

    // ---------------------------------------------------------------- placement helpers

    private delegate void PropFactory(Vector3 position, float scale);

    private bool IsDry(float x, float z) => _sampler.ChannelFactor(x, z) < 0.03f;

    private void Scatter(int sector, Terrain terrain, int count, float spacing, PropFactory factory, float edgeMargin = 0.07f, bool allowWet = false)
    {
        Span<float> w = stackalloc float[Terrains.Count];
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
            if (!allowWet && !IsDry(p.X, p.Z))
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
        Span<float> w = stackalloc float[Terrains.Count];
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
            // Check the whole footprint stays inside the terrain and out of any river.
            var ok = IsDry(p.X, p.Z) || terrain == Terrain.Water;
            for (var i = 0; i < 6 && ok; i++)
            {
                var q = p + (HexLayout.AngleToDirection(i * Mathf.Pi / 3f) * footprint);
                _sampler.TerrainWeights(q.X, q.Z, w);
                ok = w[(int)terrain] > 0.9f && (terrain == Terrain.Water || IsDry(q.X, q.Z));
            }

            if (!ok)
                continue;

            var y = terrain == Terrain.Water ? TerrainSampler.WaterLevel : _sampler.Evaluate(p.X, p.Z).Height;
            var position = new Vector3(p.X, y, p.Z);
            _occupied.Add(new Vector3(p.X, 0, p.Z));
            for (var i = 0; i < 6; i++)
                _occupied.Add(new Vector3(p.X, 0, p.Z) + (HexLayout.AngleToDirection(i * Mathf.Pi / 3f) * footprint * 0.7f));
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

    private T Pick<T>(IReadOnlyList<T> options) => options[_rng.RandiRange(0, options.Count - 1)];

    private static Color Vary(Color c, RandomNumberGenerator rng, float amount = 0.08f) =>
        new(c.R * (1 + rng.RandfRange(-amount, amount)), c.G * (1 + rng.RandfRange(-amount, amount)), c.B * (1 + rng.RandfRange(-amount, amount)));

    private float Ground(Vector3 p) => _sampler.Evaluate(p.X, p.Z).Height;

    // ---------------------------------------------------------------- ground detail

    /// <summary>Fine scatter over the whole tile: grass tufts, pebbles, and reeds where land meets water.</summary>
    private void AddGroundDetail()
    {
        Span<float> w = stackalloc float[Terrains.Count];
        for (var i = 0; i < 110; i++)
        {
            var p = new Vector3(_rng.RandfRange(-1f, 1f), 0, _rng.RandfRange(-1f, 1f)) * HexLayout.Radius;
            if (HexLayout.DistanceToHexEdge(p.X, p.Z) < 0.03f)
                continue;
            _sampler.TerrainWeights(p.X, p.Z, w);
            var sample = _sampler.Evaluate(p.X, p.Z);
            p.Y = sample.Height;
            var lakeside = sample.Wet > 0.15f && sample.Wet < 0.75f && sample.Height > TerrainSampler.WaterLevel - 0.02f;

            Begin(p);
            if (lakeside && _rng.Randf() < 0.5f)
            {
                AddReeds(p, _rng.RandfRange(0.8f, 1.2f));
                continue;
            }

            if (sample.Wet > 0.1f || sample.Channel > 0.02f)
                continue;

            var grassy = w[(int)Terrain.Grass] + w[(int)Terrain.Orchard] + (w[(int)Terrain.Village] * 0.5f) + (w[(int)Terrain.Forest] * 0.6f) + (w[(int)Terrain.Swamp] * 0.7f);
            var roll = _rng.Randf();
            if (roll < grassy * 0.85f)
                AddTuft(p, sample.Color);
            else if (roll > 0.94f && w[(int)Terrain.Snow] < 0.5f && w[(int)Terrain.Water] < 0.3f)
                AddPebble(p);
        }
    }

    private void AddTuft(Vector3 p, Color ground)
    {
        _kit.Sway = 0.9f;
        _kit.OrientFrom = null;
        var baseColor = ground.Darkened(0.12f);
        var blades = _rng.RandiRange(3, 5);
        for (var b = 0; b < blades; b++)
        {
            var yaw = _rng.Randf() * Mathf.Tau;
            var lean = _rng.RandfRange(0.15f, 0.5f);
            var h = _rng.RandfRange(0.035f, 0.06f);
            var basis = new Basis(Vector3.Up, yaw) * new Basis(Vector3.Right, lean);
            At(p + new Vector3(_rng.RandfRange(-0.012f, 0.012f), -0.004f, _rng.RandfRange(-0.012f, 0.012f)), basis);
            var tip = ground.Lightened(_rng.RandfRange(0.05f, 0.18f));
            var a = new Vector3(-0.006f, 0, 0);
            var c = new Vector3(0.006f, 0, 0);
            var t = new Vector3(0, h, 0);
            _kit.Triangle(a, c, t, Mid(baseColor, tip));
            _kit.Triangle(c, a, t, Mid(baseColor, tip).Darkened(0.08f));
        }

        static Color Mid(Color a, Color b) => a.Lerp(b, 0.5f);
    }

    private void AddPebble(Vector3 p)
    {
        At(p + new Vector3(0, -0.006f, 0), _rng.Randf() * Mathf.Tau, _rng.RandfRange(0.35f, 0.6f));
        _kit.Blob(new Vector3(0.03f, 0.02f, 0.025f), Vary(Palette.Stone, _rng, 0.06f), _rng.RandiRange(0, 9999), 0.25f);
    }

    private void AddReeds(Vector3 p, float scale)
    {
        _kit.Sway = 1f;
        var n = _rng.RandiRange(3, 6);
        for (var i = 0; i < n; i++)
        {
            var q = p + new Vector3(_rng.RandfRange(-0.03f, 0.03f), -0.01f, _rng.RandfRange(-0.03f, 0.03f));
            var lean = new Basis(Vector3.Up, _rng.Randf() * Mathf.Tau) * new Basis(Vector3.Right, _rng.RandfRange(0f, 0.25f));
            At(q, lean.Scaled(Vector3.One * scale));
            _kit.Frustum(3, 0.005f, 0.002f, _rng.RandfRange(0.07f, 0.12f), Vary(new Color("7f9a4a"), _rng));
            if (_rng.Randf() < 0.35f)
            {
                At(q + (lean * new Vector3(0, 0.08f * scale, 0)), lean.Scaled(Vector3.One * scale));
                _kit.Frustum(5, 0.007f, 0.007f, 0.025f, new Color("6b4a32"));
            }
        }
    }

    // ---------------------------------------------------------------- trees

    private void AddForestTree(Vector3 p, float scale)
    {
        if (_rng.Randf() < _style.ConiferRatio)
        {
            AddConifer(p, scale * _rng.RandfRange(0.9f, 1.25f), snowy: _style.SnowyRoofs && _rng.Randf() < 0.35f);
            return;
        }

        switch (_style.Theme)
        {
            case WorldTheme.Tropical when _rng.Randf() < 0.3f:
                AddPalm(p, scale * 1.1f);
                break;
            case WorldTheme.Savanna:
                AddAcacia(p, scale);
                break;
            case WorldTheme.Nordic:
                AddBirch(p, scale);
                break;
            default:
                AddRoundTree(p, scale * (_style.Theme == WorldTheme.Tropical ? 1.25f : 1f), Pick(_style.Leaves));
                break;
        }
    }

    private void AddUndergrowth(Vector3 p, float scale)
    {
        var roll = _rng.Randf();
        if (roll < 0.55f)
            AddBush(p, scale * 0.8f, Pick(_style.Leaves).Darkened(0.15f));
        else if (roll < 0.8f)
            AddMushrooms(p, scale);
        else
            AddLog(p, scale);
    }

    private void AddConifer(Vector3 p, float scale, bool snowy = false)
    {
        var yaw = _rng.Randf() * Mathf.Tau;
        At(p, yaw, scale);
        _kit.Sway = 0.1f;
        _kit.Frustum(5, 0.024f, 0.018f, 0.06f, Palette.TrunkBrown);
        _kit.Sway = 1f;
        var green = Vary(Pick(_style.Conifers), _rng);
        float[] radii = [0.105f, 0.083f, 0.058f];
        for (var i = 0; i < radii.Length; i++)
        {
            At(p + new Vector3(0, (0.045f + (i * 0.075f)) * scale, 0), yaw + (i * 0.4f), scale);
            var side = green.Lightened(i * 0.05f);
            var top = snowy ? Palette.Snow : green.Darkened(0.1f);
            _kit.Frustum(7, radii[i], 0.008f, 0.13f, side, top, capBottom: true, bottom: green.Darkened(0.25f));
            if (snowy)
            {
                At(p + new Vector3(0, (0.12f + (i * 0.075f)) * scale, 0), yaw + (i * 0.4f), scale);
                _kit.Cone(7, radii[i] * 0.45f, 0.055f, Palette.Snow);
            }
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

    private void AddBirch(Vector3 p, float scale)
    {
        var yaw = _rng.Randf() * Mathf.Tau;
        At(p, yaw, scale);
        _kit.Sway = 0.15f;
        _kit.Frustum(5, 0.014f, 0.01f, 0.2f, new Color("eeeae0"));
        _kit.Sway = 1f;
        var leaf = Pick(_style.Leaves);
        for (var i = 0; i < 3; i++)
        {
            At(p + new Vector3(_rng.RandfRange(-0.02f, 0.02f), (0.16f + (i * 0.05f)) * scale, _rng.RandfRange(-0.02f, 0.02f)), yaw, scale);
            _kit.Blob(new Vector3(0.05f, 0.06f, 0.05f) * (1f - (i * 0.2f)), Vary(leaf, _rng), _rng.RandiRange(0, 9999));
        }
    }

    private void AddAcacia(Vector3 p, float scale)
    {
        var yaw = _rng.Randf() * Mathf.Tau;
        At(p, yaw, scale);
        _kit.Sway = 0.1f;
        _kit.Frustum(5, 0.016f, 0.012f, 0.16f, new Color("6b4a32"));
        var basis = new Basis(Vector3.Up, yaw);
        At(p + (basis * new Vector3(0.03f, 0.12f, 0) * scale), new Basis(Vector3.Up, yaw) * new Basis(Vector3.Forward, -0.6f) * Basis.Identity.Scaled(Vector3.One * scale));
        _kit.Frustum(4, 0.009f, 0.006f, 0.07f, new Color("6b4a32"));
        _kit.Sway = 0.8f;
        At(p + new Vector3(0, 0.17f * scale, 0), yaw, scale);
        _kit.Frustum(9, 0.13f, 0.11f, 0.035f, Vary(Pick(_style.Leaves), _rng), Vary(Pick(_style.Leaves), _rng).Lightened(0.08f), capBottom: true, bottom: Pick(_style.Leaves).Darkened(0.25f));
    }

    private void AddBush(Vector3 p, float scale, Color color)
    {
        At(p, _rng.Randf() * Mathf.Tau, scale);
        _kit.Sway = 0.5f;
        _kit.Blob(new Vector3(0.05f, 0.035f, 0.045f), Vary(color, _rng), _rng.RandiRange(0, 9999));
    }

    private void AddMushrooms(Vector3 p, float scale)
    {
        for (var i = 0; i < _rng.RandiRange(2, 3); i++)
        {
            var q = p + new Vector3(_rng.RandfRange(-0.025f, 0.025f), 0, _rng.RandfRange(-0.025f, 0.025f));
            q.Y = Ground(q);
            At(q, 0, scale * _rng.RandfRange(0.7f, 1.2f));
            _kit.Frustum(5, 0.006f, 0.005f, 0.02f, new Color("f1ead8"));
            At(q + new Vector3(0, 0.018f * scale, 0), 0, scale * _rng.RandfRange(0.7f, 1.2f));
            _kit.Frustum(6, 0.016f, 0.0f, 0.014f, new Color("d64545"));
        }
    }

    private void AddLog(Vector3 p, float scale)
    {
        var yaw = _rng.Randf() * Mathf.Tau;
        var basis = new Basis(Vector3.Up, yaw) * new Basis(Vector3.Forward, Mathf.Pi / 2f);
        At(p + new Vector3(0, 0.014f, 0) + (new Basis(Vector3.Up, yaw) * new Vector3(-0.05f * scale, 0, 0)), basis.Scaled(Vector3.One * scale));
        _kit.Frustum(6, 0.014f, 0.013f, 0.1f, new Color("7b5537"), new Color("c8a070"), capBottom: true, bottom: new Color("c8a070"));
    }

    private void AddFlowers(Vector3 p, float scale)
    {
        var c = Pick(_style.Flowers);
        var n = _rng.RandiRange(4, 7);
        _kit.Sway = 0.7f;
        for (var i = 0; i < n; i++)
        {
            var q = p + new Vector3(_rng.RandfRange(-0.05f, 0.05f), 0, _rng.RandfRange(-0.05f, 0.05f));
            q.Y = Ground(q);
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

    private void AddMeadowTree(Vector3 p, float scale)
    {
        switch (_style.MeadowTree)
        {
            case TreeStyle.Birch:
                AddBirch(p, scale);
                break;
            case TreeStyle.Palm:
                AddPalm(p, scale);
                break;
            case TreeStyle.Acacia:
                AddAcacia(p, scale);
                break;
            default:
                AddRoundTree(p, scale * 0.9f, Pick(_style.Leaves).Lightened(0.08f));
                break;
        }
    }

    private void AddMeadowDetail(Vector3 p, float scale)
    {
        var roll = _rng.Randf();
        if (roll < 0.45f)
            AddFlowers(p, scale);
        else if (roll < 0.68f)
            AddBush(p, scale, Pick(_style.Leaves).Lightened(0.1f));
        else if (roll < 0.86f)
            AddMeadowTree(p, scale);
        else
            AddRock(p, scale * 0.7f, Palette.Stone);
    }

    // ---------------------------------------------------------------- farmland & orchards

    private void AddFieldDetail(Vector3 p, float scale)
    {
        var roll = _rng.Randf();
        if (roll < 0.6f)
        {
            // Hay bale lying on its side.
            var yaw = _rng.Randf() * Mathf.Tau;
            var basis = new Basis(Vector3.Up, yaw) * new Basis(Vector3.Forward, Mathf.Pi / 2f);
            At(p + new Vector3(0, 0.035f * scale, 0) + (new Basis(Vector3.Up, yaw) * new Vector3(-0.035f * scale, 0, 0)), basis.Scaled(Vector3.One * scale));
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

    /// <summary>Orchard trees sit on a world-aligned lattice so rows continue across neighbouring tiles.</summary>
    private void PlantOrchardRows()
    {
        Span<float> w = stackalloc float[Terrains.Count];
        const float spacing = 0.26f;
        var centre = _sampler.ToWorld(0, 0, 0);
        var toLocal = _sampler.ToLocalTransform;
        var fruit = _style.Theme is WorldTheme.Autumn or WorldTheme.Savanna;
        var x0 = Mathf.Floor((centre.X - 1.1f) / spacing) * spacing;
        var z0 = Mathf.Floor((centre.Z - 1.1f) / spacing) * spacing;
        for (var wx = x0; wx < centre.X + 1.1f; wx += spacing)
        {
            for (var wz = z0; wz < centre.Z + 1.1f; wz += spacing)
            {
                // Offset alternate rows for a natural staggered look.
                var row = Mathf.RoundToInt(wz / spacing);
                var local = toLocal * new Vector3(wx + ((row & 1) * spacing * 0.5f), 0, wz);
                if (HexLayout.DistanceToHexEdge(local.X, local.Z) < 0.08f)
                    continue;
                _sampler.TerrainWeights(local.X, local.Z, w);
                if (w[(int)Terrain.Orchard] < 0.85f || !IsDry(local.X, local.Z))
                    continue;
                local.Y = Ground(local);
                _occupied.Add(new Vector3(local.X, 0, local.Z));
                Begin(local);
                AddFruitTree(local, _rng.RandfRange(0.8f, 1.0f), fruit);
            }
        }
    }

    private void AddFruitTree(Vector3 p, float scale, bool fruit)
    {
        var yaw = _rng.Randf() * Mathf.Tau;
        At(p, yaw, scale);
        _kit.Sway = 0.1f;
        _kit.Frustum(5, 0.018f, 0.014f, 0.09f, Palette.TrunkBrown);
        _kit.Sway = 0.8f;
        At(p + new Vector3(0, 0.12f * scale, 0), yaw, scale);
        Color[] leafy = [new("5f9e45"), new("6aa84c")];
        var canopy = fruit ? Pick(leafy) : Pick(_style.Blossoms);
        _kit.Blob(new Vector3(0.075f, 0.065f, 0.075f), Vary(canopy, _rng, 0.05f), _rng.RandiRange(0, 9999));
        var dots = fruit ? Pick(_style.Blossoms) : Colors.White;
        for (var i = 0; i < 4; i++)
        {
            var a = _rng.Randf() * Mathf.Tau;
            var d = new Vector3(Mathf.Cos(a) * 0.065f, _rng.RandfRange(-0.02f, 0.04f), Mathf.Sin(a) * 0.065f);
            At(p + new Vector3(0, 0.12f * scale, 0) + (d * scale), 0, scale);
            _kit.Blob(new Vector3(0.012f, 0.012f, 0.012f), dots, i, 0.05f);
        }
    }

    private void AddWindmill(Vector3 p, float scale)
    {
        var yaw = _rng.Randf() * Mathf.Tau;
        var wall = Pick(_style.Walls);
        At(p, yaw, scale);
        _kit.Frustum(8, 0.075f, 0.05f, 0.32f, wall, wall.Darkened(0.1f));
        At(p + new Vector3(0, 0.32f, 0), yaw, scale);
        _kit.Cone(8, 0.065f, 0.08f, Pick(_style.Roofs));
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

    private Color RoofColor()
    {
        var roof = Vary(Pick(_style.Roofs), _rng, 0.06f);
        return _style.SnowyRoofs ? roof.Lerp(Palette.Snow, 0.75f) : roof;
    }

    private void AddHouse(Vector3 p, float scale)
    {
        // Face roughly towards the tile centre with a little jitter, like streets radiating out.
        var toCentre = Mathf.Atan2(p.X, p.Z);
        var yaw = toCentre + (Mathf.Round(_rng.RandfRange(-1f, 1f)) * Mathf.Pi / 2f) + _rng.RandfRange(-0.15f, 0.15f);
        var w = _rng.RandfRange(0.11f, 0.15f);
        var d = _rng.RandfRange(0.085f, 0.105f);
        var h = _rng.RandfRange(0.07f, 0.1f);
        var roof = RoofColor();
        var wall = Pick(_style.Walls);

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

        // Door and a lit window.
        var basis = new Basis(Vector3.Up, yaw);
        At(p + (basis * new Vector3(0, 0, d * 0.5f + 0.001f) * scale), yaw, scale);
        _kit.Box(new Vector3(0.022f, 0.04f, 0.003f), new Color("6b4a32"));
        At(p + (basis * new Vector3(w * 0.3f, h * 0.45f, d * 0.5f + 0.001f) * scale), yaw, scale);
        _kit.Box(new Vector3(0.02f, 0.02f, 0.003f), new Color("ffe7a3"));
    }

    private void AddChurch(Vector3 p, float scale)
    {
        var yaw = _rng.Randf() * Mathf.Tau;
        var wall = Pick(_style.Walls);
        var roof = RoofColor();
        At(p + new Vector3(0, -0.01f, 0), yaw, scale);
        _kit.Box(new Vector3(0.22f, 0.13f, 0.13f), wall);
        At(p + new Vector3(0, 0.12f, 0), yaw, scale);
        _kit.GableRoof(0.24f, 0.16f, 0.08f, roof, wall.Darkened(0.05f));
        var towerOffset = new Basis(Vector3.Up, yaw) * new Vector3(-0.13f, 0, 0) * scale;
        At(p + towerOffset + new Vector3(0, -0.01f, 0), yaw, scale);
        _kit.Box(new Vector3(0.08f, 0.27f, 0.08f), wall);
        At(p + towerOffset + new Vector3(0, 0.26f * scale, 0), yaw + (Mathf.Pi / 4f), scale);
        _kit.Frustum(4, 0.065f, 0f, 0.16f, roof);
        At(p + towerOffset + (new Basis(Vector3.Up, yaw) * new Vector3(0, 0.19f, 0.041f) * scale), yaw, scale);
        _kit.Box(new Vector3(0.03f, 0.04f, 0.003f), Palette.Gold);
    }

    private void AddWell(Vector3 p, float scale)
    {
        At(p, 0, scale);
        _kit.Frustum(8, 0.04f, 0.04f, 0.035f, Palette.Stone, new Color("3a6b8a"));
        for (var side = -1; side <= 1; side += 2)
        {
            At(p + new Vector3(side * 0.032f, 0, 0) * scale, 0, scale);
            _kit.Box(new Vector3(0.008f, 0.09f, 0.008f), Palette.TrunkBrown);
        }

        At(p + new Vector3(0, 0.085f * scale, 0), 0, scale);
        _kit.GableRoof(0.09f, 0.07f, 0.035f, RoofColor(), Palette.TrunkBrown);
    }

    private void AddVillageGreen(Vector3 p, float scale)
    {
        var roll = _rng.Randf();
        if (roll < 0.45f)
            AddMeadowTree(p, scale * 0.85f);
        else if (roll < 0.75f)
            AddBush(p, scale, Pick(_style.Leaves));
        else
            AddFence(p, scale);
    }

    private void AddFence(Vector3 p, float scale)
    {
        var yaw = _rng.Randf() * Mathf.Tau;
        var basis = new Basis(Vector3.Up, yaw);
        for (var i = 0; i < 4; i++)
        {
            var q = p + (basis * new Vector3((i - 1.5f) * 0.035f, 0, 0) * scale);
            q.Y = Ground(q);
            At(q, yaw, scale);
            _kit.Box(new Vector3(0.007f, 0.035f, 0.007f), new Color("c8a070"));
        }

        At(p + new Vector3(0, 0.022f * scale, 0), yaw, scale);
        _kit.Box(new Vector3(0.12f, 0.006f, 0.004f), new Color("c8a070"));
    }

    // ---------------------------------------------------------------- water

    private void AddWaterDetail(Vector3 p, float scale)
    {
        var roll = _rng.Randf();
        var surface = new Vector3(p.X, TerrainSampler.WaterLevel + 0.003f, p.Z);
        if (roll < 0.55f)
            AddLilyPads(surface, scale);
        else
            AddRock(surface + new Vector3(0, -0.01f, 0), scale * 0.9f, Palette.StoneDark);
    }

    private void AddLilyPads(Vector3 surface, float scale)
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

    // ---------------------------------------------------------------- mountains, dunes, snow, marsh

    private void AddMountainDetail(Vector3 p, float scale)
    {
        if (p.Y < 0.3f && _rng.Randf() < 0.75f)
            AddConifer(p, scale * 0.8f, snowy: _style.SnowyRoofs);
        else if (p.Y < 0.55f)
            AddRock(p, scale * 1.2f, Palette.StoneDark);
    }

    private void AddDesertDetail(Vector3 p, float scale)
    {
        Span<float> w = stackalloc float[Terrains.Count];
        // Palms near water, cacti inland.
        var nearWater = false;
        for (var i = 0; i < 6 && !nearWater; i++)
        {
            var q = p + (HexLayout.AngleToDirection(i * Mathf.Pi / 3f) * 0.22f);
            _sampler.TerrainWeights(q.X, q.Z, w);
            nearWater = w[(int)Terrain.Water] > 0.3f || _sampler.ChannelFactor(q.X, q.Z) > 0.3f;
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
            var c = Vary(Pick(_style.Leaves), _rng);
            _kit.Triangle(new Vector3(-0.02f, 0, 0), new Vector3(0.02f, 0, 0), new Vector3(0, -0.04f, 0.13f), c);
            _kit.Triangle(new Vector3(0.02f, 0, 0), new Vector3(-0.02f, 0, 0), new Vector3(0, -0.04f, 0.13f), c.Darkened(0.15f));
        }

        _kit.Transform = new Transform3D(Basis.Identity, top);
        _kit.Blob(new Vector3(0.02f, 0.018f, 0.02f), new Color("6b4a32"), 3);
    }

    private void AddSnowDetail(Vector3 p, float scale)
    {
        var roll = _rng.Randf();
        if (roll < 0.7f)
            AddConifer(p, scale * _rng.RandfRange(0.85f, 1.2f), snowy: true);
        else if (roll < 0.9f)
            AddRock(p, scale, new Color("d8dee6"));
        else
            AddBush(p, scale * 0.7f, new Color("e9eef3"));
    }

    private void AddSnowman(Vector3 p, float scale)
    {
        At(p, 0, scale);
        _kit.Blob(new Vector3(0.035f, 0.032f, 0.035f), Palette.Snow, 1, 0.05f);
        At(p + new Vector3(0, 0.05f * scale, 0), 0, scale);
        _kit.Blob(new Vector3(0.025f, 0.023f, 0.025f), Palette.Snow, 2, 0.05f);
        At(p + new Vector3(0, 0.08f * scale, 0), 0, scale);
        _kit.Blob(new Vector3(0.018f, 0.017f, 0.018f), Palette.Snow, 3, 0.05f);
        var basis = new Basis(Vector3.Up, _rng.Randf() * Mathf.Tau);
        At(p + new Vector3(0, 0.08f * scale, 0) + (basis * new Vector3(0, 0, 0.016f) * scale), basis * new Basis(Vector3.Right, Mathf.Pi / 2f));
        _kit.Cone(5, 0.005f, 0.02f, new Color("f08a24"));
        At(p + new Vector3(0, 0.095f * scale, 0), 0, scale);
        _kit.Frustum(6, 0.013f, 0.013f, 0.02f, new Color("2b2b2b"), new Color("2b2b2b"));
    }

    private void AddCabin(Vector3 p, float scale)
    {
        var yaw = _rng.Randf() * Mathf.Tau;
        var wall = new Color("8b5a3c");
        At(p + new Vector3(0, -0.01f, 0), yaw, scale);
        _kit.Box(new Vector3(0.14f, 0.09f, 0.1f), wall, wall.Darkened(0.1f));
        At(p + new Vector3(0, 0.08f, 0), yaw, scale);
        _kit.GableRoof(0.17f, 0.14f, 0.07f, Palette.Snow, wall.Darkened(0.1f));
        var basis = new Basis(Vector3.Up, yaw);
        At(p + (basis * new Vector3(0.04f, 0.04f, 0.051f) * scale), yaw, scale);
        _kit.Box(new Vector3(0.022f, 0.022f, 0.003f), new Color("ffd77a"));
        At(p + (basis * new Vector3(-0.04f, 0.1f, 0.0f) * scale), yaw, scale);
        _kit.Box(new Vector3(0.02f, 0.06f, 0.02f), Palette.StoneDark);
    }

    private void AddSwampDetail(Vector3 p, float scale)
    {
        if (p.Y < TerrainSampler.WaterLevel)
        {
            if (_rng.Randf() < 0.6f)
                AddLilyPads(new Vector3(p.X, TerrainSampler.WaterLevel + 0.003f, p.Z), scale * 0.8f);
            return;
        }

        var roll = _rng.Randf();
        if (roll < 0.45f)
            AddReeds(p, scale * 1.2f);
        else if (roll < 0.68f)
            AddDeadTree(p, scale * 1.2f);
        else if (roll < 0.8f)
            AddBush(p, scale * 0.8f, new Color("4f6a3a"));
        else
            AddMushrooms(p, scale);
    }

    private void AddDeadTree(Vector3 p, float scale)
    {
        var yaw = _rng.Randf() * Mathf.Tau;
        var bark = new Color("6e6252");
        At(p, yaw, scale);
        _kit.Sway = 0.15f;
        _kit.Frustum(5, 0.018f, 0.008f, 0.2f, bark);
        for (var i = 0; i < 3; i++)
        {
            var branchYaw = yaw + (i * 2.1f);
            var basis = new Basis(Vector3.Up, branchYaw) * new Basis(Vector3.Forward, 0.9f + (i * 0.15f));
            At(p + new Vector3(0, (0.08f + (i * 0.04f)) * scale, 0), basis.Scaled(Vector3.One * scale));
            _kit.Frustum(4, 0.007f, 0.002f, 0.07f, bark);
        }
    }
}
