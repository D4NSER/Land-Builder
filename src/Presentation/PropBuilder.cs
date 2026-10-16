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
    WaterWheel
}

public sealed record AnimatedPart(AnimatedPartKind Kind, Node3D Node, float Phase, Vector3 BasePosition);

/// <summary>
/// Scatters low-poly decorations over each sector (trees, buildings, hay, rocks, boats...) plus a fine ground-detail
/// pass (grass tufts, pebbles, reeds). Everything static is merged into one mesh per tile; CUSTOM0 stores each
/// prop's pivot and pop-in delay so the shader can grow them in, and vertex alpha carries the <see cref="Sway"/>
/// class used for wind and seasons. The island's theme picks species, architecture and colours.
/// Split across partial files: trees, buildings and nature.
/// </summary>
public sealed partial class PropBuilder
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
        int Count(Terrain t) => counts.GetValueOrDefault(t);

        // Landmarks first so they claim the best spot.
        if (Count(Terrain.Village) >= 3 && _rng.Randf() < 0.75f)
            TryLandmark(Terrain.Village, (p, s) => AddVillageLandmark(p, s * BuildingScale), 0.26f);
        else if (Count(Terrain.Village) >= 2 && _rng.Randf() < 0.4f)
            TryLandmark(Terrain.Village, AddVillageSmallLandmark, 0.09f);
        if (_sampler.HasRiver && _rng.Randf() < 0.55f)
            TryWaterMill();
        if (Count(Terrain.Field) >= 3 && _rng.Randf() < 0.6f)
            TryLandmark(Terrain.Field, AddFarmLandmark, 0.18f);
        if (Count(Terrain.Water) >= 4 && _rng.Randf() < 0.45f)
            TryLandmark(Terrain.Water, AddBoat, 0.15f);
        if (Count(Terrain.Mountain) >= 2)
            AddPeaks(Count(Terrain.Mountain));
        if (Count(Terrain.Snow) >= 2 && _rng.Randf() < 0.25f)
            TryLandmark(Terrain.Snow, AddSnowman, 0.06f);
        if (Count(Terrain.Snow) >= 3 && _rng.Randf() < 0.35f)
            TryLandmark(Terrain.Snow, AddCabin, 0.12f);
        if (_style.Architecture == Architecture.Japanese && (Count(Terrain.Forest) >= 2 || Count(Terrain.Water) >= 2) && _rng.Randf() < 0.3f)
            TryLandmark(Count(Terrain.Forest) >= 2 ? Terrain.Forest : Terrain.Grass, AddTorii, 0.08f);

        if (Count(Terrain.Orchard) > 0)
            PlantOrchardRows();
        if (_style.RicePaddies && Count(Terrain.Field) > 0)
            PlantRice();

        var dense = _style.Rainforest ? 1.45f : 1f;
        for (var sector = 0; sector < 6; sector++)
        {
            var terrain = _sampler.PropTerrain(sector);
            var bank = _sampler.SectorTerrain(sector) == Terrain.River;
            int N(int min, int max) => bank ? Mathf.Max(1, _rng.RandiRange(min, max) / 2) : _rng.RandiRange(min, max);
            switch (terrain)
            {
                case Terrain.Forest:
                    Scatter(sector, terrain, Mathf.RoundToInt(N(8, 11) * dense), _style.Rainforest ? 0.07f : 0.08f, AddForestTree);
                    Scatter(sector, terrain, Mathf.RoundToInt(N(2, 4) * dense), 0.05f, AddUndergrowth);
                    break;
                case Terrain.Grass:
                    Scatter(sector, terrain, N(3, 6), 0.06f, AddMeadowDetail);
                    break;
                case Terrain.Field when !_style.RicePaddies:
                    Scatter(sector, terrain, N(1, 3), 0.1f, AddFieldDetail);
                    break;
                case Terrain.Village:
                    Scatter(sector, terrain, N(2, 4), 0.17f, (p, s) => AddHouse(p, s * BuildingScale));
                    Scatter(sector, terrain, N(1, 3), 0.06f, AddVillageGreen);
                    break;
                case Terrain.Water:
                    Scatter(sector, terrain, N(1, 3), 0.07f, AddWaterDetail, allowWet: true);
                    break;
                case Terrain.Mountain:
                    Scatter(sector, terrain, N(3, 5), 0.08f, AddMountainDetail, edgeMargin: 0.04f);
                    break;
                case Terrain.Sand:
                    Scatter(sector, terrain, N(2, 3), 0.1f, AddDesertDetail);
                    break;
                case Terrain.Snow:
                    Scatter(sector, terrain, N(3, 6), 0.08f, AddSnowDetail);
                    break;
                case Terrain.Swamp:
                    Scatter(sector, terrain, N(7, 10), 0.05f, AddSwampDetail, allowWet: true);
                    break;
            }
        }

        AddGroundDetail();
        return _kit.IsEmpty ? null : _kit.Commit(withCustom: true);
    }

    // ---------------------------------------------------------------- placement helpers

    private delegate void PropFactory(Vector3 position, float scale);

    /// <summary>Buildings are drawn a little larger than life so architecture reads from the default camera.</summary>
    private const float BuildingScale = 1.25f;

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

    private bool TryLandmark(Terrain terrain, PropFactory factory, float footprint)
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
            Claim(position, footprint);
            Begin(position);
            factory(position, 1f);
            return true;
        }

        return false;
    }

    private void Claim(Vector3 p, float footprint)
    {
        _occupied.Add(new Vector3(p.X, 0, p.Z));
        for (var i = 0; i < 6; i++)
            _occupied.Add(new Vector3(p.X, 0, p.Z) + (HexLayout.AngleToDirection(i * Mathf.Pi / 3f) * footprint * 0.7f));
    }

    /// <summary>Sets the pop-in pivot and delay. Props near the centre appear first, rippling outward.</summary>
    private void Begin(Vector3 pivot)
    {
        var dist = new Vector2(pivot.X, pivot.Z).Length();
        var delay = Mathf.Clamp((dist / HexLayout.Radius * 0.75f) + _rng.RandfRange(0f, 0.25f), 0f, 1f);
        _kit.Custom = new Color(pivot.X, pivot.Y, pivot.Z, delay);
        _kit.Sway = Sway.Rigid;
    }

    private void At(Vector3 position, float yaw, float scale) =>
        _kit.Transform = new Transform3D(new Basis(Vector3.Up, yaw).Scaled(Vector3.One * scale), position);

    private void At(Vector3 position, Basis basis) => _kit.Transform = new Transform3D(basis, position);

    private T Pick<T>(IReadOnlyList<T> options) => options[_rng.RandiRange(0, options.Count - 1)];

    private T PickWeighted<T>(IReadOnlyList<(T Item, float Weight)> options)
    {
        var total = options.Sum(o => o.Weight);
        var roll = _rng.Randf() * total;
        foreach (var (item, weight) in options)
        {
            roll -= weight;
            if (roll <= 0f)
                return item;
        }

        return options[^1].Item;
    }

    private static Color Vary(Color c, RandomNumberGenerator rng, float amount = 0.08f) =>
        new(c.R * (1 + rng.RandfRange(-amount, amount)), c.G * (1 + rng.RandfRange(-amount, amount)), c.B * (1 + rng.RandfRange(-amount, amount)));

    private float Ground(Vector3 p) => _sampler.Evaluate(p.X, p.Z).Height;

    // ---------------------------------------------------------------- ground detail

    /// <summary>Fine scatter over the whole tile: grass tufts, pebbles, and reeds where land meets water.</summary>
    private void AddGroundDetail()
    {
        Span<float> w = stackalloc float[Terrains.Count];
        var tries = _style.Rainforest ? 140 : 110;
        for (var i = 0; i < tries; i++)
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
        _kit.Sway = Sway.Grass;
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
            _kit.Triangle(a, c, t, baseColor.Lerp(tip, 0.5f));
            _kit.Triangle(c, a, t, baseColor.Lerp(tip, 0.5f).Darkened(0.08f));
        }
    }

    private void AddPebble(Vector3 p)
    {
        At(p + new Vector3(0, -0.006f, 0), _rng.Randf() * Mathf.Tau, _rng.RandfRange(0.35f, 0.6f));
        _kit.Blob(new Vector3(0.03f, 0.02f, 0.025f), Vary(Palette.Stone, _rng, 0.06f), _rng.RandiRange(0, 9999), 0.25f);
    }

    private void AddReeds(Vector3 p, float scale)
    {
        var n = _rng.RandiRange(3, 6);
        for (var i = 0; i < n; i++)
        {
            _kit.Sway = Sway.Grass;
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
}
