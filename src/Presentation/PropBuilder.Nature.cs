using System;
using Godot;
using LandBuilder.Domain;

namespace LandBuilder.Presentation;

/// <summary>Peaks, fields, orchards, rice paddies, water, dunes, snowfields and marshes.</summary>
public sealed partial class PropBuilder
{
    // ---------------------------------------------------------------- mountains

    /// <summary>
    /// Craggy spires (with snow caps where the climate allows) on the highest ground of a mountain tile.
    /// Sakura Shores gets a single Fuji-like cone instead.
    /// </summary>
    private void AddPeaks(int mountainSectors)
    {
        Span<float> w = stackalloc float[Terrains.Count];
        var best = Vector3.Zero;
        var bestHeight = float.MinValue;
        for (var i = 0; i < 60; i++)
        {
            var p = new Vector3(_rng.RandfRange(-0.7f, 0.7f), 0, _rng.RandfRange(-0.7f, 0.7f));
            if (HexLayout.DistanceToHexEdge(p.X, p.Z) < 0.25f)
                continue;
            _sampler.TerrainWeights(p.X, p.Z, w);
            if (w[(int)Terrain.Mountain] < 0.95f || !IsDry(p.X, p.Z))
                continue;
            var h = Ground(p);
            if (h > bestHeight)
            {
                bestHeight = h;
                best = p with { Y = h };
            }
        }

        if (bestHeight == float.MinValue)
            return;

        Claim(best, 0.14f);
        Begin(best);
        _kit.Sway = Sway.Rigid;
        var snowy = _style.Seasons.Snow > 0.3f || _style.SnowLine < 0.65f;
        var size = Mathf.Lerp(0.8f, 1.35f, (mountainSectors - 2) / 4f);
        var rock = Vary(_style.GroundB(Terrain.Mountain), _rng, 0.05f);

        if (_style.Theme == WorldTheme.Japanese)
        {
            Spire(best - new Vector3(0, 0.04f, 0), 0.24f * size, 0.42f * size, 12, rock, true, 0.38f);
            return;
        }

        Spire(best - new Vector3(0, 0.03f, 0), 0.13f * size, 0.42f * size, 6, rock, snowy, 0.42f);
        for (var i = 0; i < 2; i++)
        {
            var a = _rng.Randf() * Mathf.Tau;
            var q = best + (new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * 0.11f * size);
            q.Y = Ground(q) - 0.03f;
            Spire(q, 0.085f * size, _rng.RandfRange(0.2f, 0.3f) * size, 5, rock.Darkened(0.06f), snowy, 0.38f);
        }
    }

    /// <summary>A faceted cone; the top <paramref name="capFraction"/> is a matching snow cap.</summary>
    private void Spire(Vector3 basePos, float radius, float height, int sides, Color rock, bool snowCap, float capFraction)
    {
        var twist = _rng.Randf() * Mathf.Tau;
        At(basePos, Basis.Identity);
        _kit.Frustum(sides, radius, 0f, height, rock, twist: twist);
        if (!snowCap)
            return;
        var capHeight = height * capFraction;
        At(basePos + new Vector3(0, height - capHeight - 0.002f, 0), Basis.Identity.Scaled(Vector3.One * 1.03f));
        _kit.Frustum(sides, radius * capFraction, 0f, capHeight, Palette.Snow, twist: twist);
    }

    private void AddMountainDetail(Vector3 p, float scale)
    {
        if (p.Y < 0.3f && _rng.Randf() < 0.75f)
            AddTree(_style.Theme == WorldTheme.Japanese ? TreeSpecies.BlackPine : TreeSpecies.Fir, p, scale * 0.8f);
        else if (p.Y < 0.55f)
            AddRock(p, scale * 1.2f, Palette.StoneDark);
    }

    // ---------------------------------------------------------------- farmland

    private void AddFieldDetail(Vector3 p, float scale)
    {
        var roll = _rng.Randf();
        if (roll < 0.55f)
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
            for (var i = 0; i < 3; i++)
            {
                _kit.Sway = Sway.Grass;
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

    /// <summary>Rows of rice seedlings standing in the flooded paddies (Sakura Shores fields).</summary>
    private void PlantRice()
    {
        Span<float> w = stackalloc float[Terrains.Count];
        const float spacing = 0.075f;
        var centre = _sampler.ToWorld(0, 0, 0);
        var toLocal = _sampler.ToLocalTransform;
        var x0 = Mathf.Floor((centre.X - 1.05f) / spacing) * spacing;
        var z0 = Mathf.Floor((centre.Z - 1.05f) / spacing) * spacing;
        for (var wx = x0; wx < centre.X + 1.05f; wx += spacing)
        {
            for (var wz = z0; wz < centre.Z + 1.05f; wz += spacing)
            {
                var local = toLocal * new Vector3(wx, 0, wz);
                if (HexLayout.DistanceToHexEdge(local.X, local.Z) < 0.04f)
                    continue;
                _sampler.TerrainWeights(local.X, local.Z, w);
                if (w[(int)Terrain.Field] < 0.85f || !IsDry(local.X, local.Z))
                    continue;
                var s = _sampler.Evaluate(local.X, local.Z);
                if (s.Height > TerrainSampler.WaterLevel)
                    continue;
                local.Y = TerrainSampler.WaterLevel - 0.012f;
                Begin(local);
                _kit.Sway = Sway.Grass;
                At(local, _rng.Randf() * Mathf.Tau, _rng.RandfRange(0.8f, 1.1f));
                _kit.Cone(4, 0.011f, 0.05f, Vary(new Color("8cc65a"), _rng, 0.08f));
            }
        }
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
                if (_style.Theme == WorldTheme.Japanese)
                    AddCherry(local, _rng.RandfRange(0.85f, 1.05f));
                else
                    AddFruitTree(local, _rng.RandfRange(0.8f, 1.0f), fruit);
            }
        }
    }

    private void AddFruitTree(Vector3 p, float scale, bool fruit)
    {
        var yaw = _rng.Randf() * Mathf.Tau;
        Trunk(p, yaw, scale, 0.018f, 0.014f, 0.09f);
        _kit.Sway = fruit ? Sway.Deciduous : Sway.Flower;
        At(p + new Vector3(0, 0.12f * scale, 0), yaw, scale);
        Color[] leafy = [new("5f9e45"), new("6aa84c")];
        var canopy = fruit ? Pick(leafy) : Pick(_style.Blossoms);
        _kit.Blob(new Vector3(0.075f, 0.065f, 0.075f), Vary(canopy, _rng, 0.05f), _rng.RandiRange(0, 9999));
        var dots = fruit ? Pick(_style.Blossoms) : Colors.White;
        for (var i = 0; i < 4; i++)
        {
            _kit.Sway = Sway.Flower;
            var a = _rng.Randf() * Mathf.Tau;
            var d = new Vector3(Mathf.Cos(a) * 0.065f, _rng.RandfRange(-0.02f, 0.04f), Mathf.Sin(a) * 0.065f);
            At(p + new Vector3(0, 0.12f * scale, 0) + (d * scale), 0, scale);
            _kit.Blob(new Vector3(0.012f, 0.012f, 0.012f), dots, i, 0.05f);
        }
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
            _kit.Sway = Sway.Rigid;
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
        kit.Box(new Vector3(0.18f, 0.035f, 0.07f), hull, hull.Lightened(0.15f));
        kit.Transform = new Transform3D(Basis.Identity, new Vector3(0.09f, 0, 0));
        kit.Frustum(3, 0.035f, 0.0f, 0.035f, hull);
        kit.Transform = new Transform3D(Basis.Identity, new Vector3(0, 0.03f, 0));
        kit.Box(new Vector3(0.008f, 0.17f, 0.008f), Palette.TrunkBrown);
        kit.Transform = new Transform3D(Basis.Identity, new Vector3(0.0f, 0.06f, 0f));
        kit.OrientFrom = null;
        var sail = _style.Architecture == Architecture.Japanese ? new Color("e9dcc0") : Palette.Wall;
        kit.Triangle(new Vector3(0.005f, 0.0f, 0), new Vector3(0.09f, 0.0f, 0), new Vector3(0.005f, 0.13f, 0), sail);
        kit.Triangle(new Vector3(0.005f, 0.0f, 0), new Vector3(0.005f, 0.13f, 0), new Vector3(0.09f, 0.0f, 0), sail);

        var node = new Node3D { Name = "Boat" };
        node.AddChild(new MeshInstance3D { Mesh = kit.Commit(withCustom: true) });
        node.Position = p + new Vector3(0, -0.012f, 0);
        node.Rotation = new Vector3(0, _rng.Randf() * Mathf.Tau, 0);
        _partsRoot.AddChild(node);
        _parts.Add(new AnimatedPart(AnimatedPartKind.Boat, node, _rng.Randf() * Mathf.Tau, node.Position));
    }

    // ---------------------------------------------------------------- dunes

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
        _kit.Sway = Sway.Rigid;
        At(p, yaw, scale);
        _kit.Frustum(6, 0.022f, 0.02f, 0.16f, Palette.Cactus, Palette.Cactus.Lightened(0.1f));
        var basis = new Basis(Vector3.Up, yaw);
        At(p + (basis * new Vector3(0.025f, 0.06f, 0) * scale), yaw, scale);
        _kit.Frustum(5, 0.012f, 0.012f, 0.06f, Palette.Cactus);
        At(p + (basis * new Vector3(-0.025f, 0.09f, 0) * scale), yaw, scale);
        _kit.Frustum(5, 0.011f, 0.011f, 0.045f, Palette.Cactus);
    }

    // ---------------------------------------------------------------- snow

    private void AddSnowDetail(Vector3 p, float scale)
    {
        var roll = _rng.Randf();
        if (roll < 0.45f)
            AddFir(p, scale * _rng.RandfRange(0.9f, 1.25f), snowy: true);
        else if (roll < 0.7f)
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

    // ---------------------------------------------------------------- marsh

    private void AddSwampDetail(Vector3 p, float scale)
    {
        if (p.Y < TerrainSampler.WaterLevel)
        {
            if (_rng.Randf() < 0.6f)
                AddLilyPads(new Vector3(p.X, TerrainSampler.WaterLevel + 0.003f, p.Z), scale * 0.8f);
            return;
        }

        var roll = _rng.Randf();
        if (roll < 0.4f)
            AddReeds(p, scale * 1.2f);
        else if (roll < 0.58f)
            AddDeadTree(p, scale * 1.2f);
        else if (roll < 0.72f)
            AddWillow(p, scale);
        else if (roll < 0.86f)
            AddBush(p, scale * 0.8f, new Color("4f6a3a"));
        else
            AddMushrooms(p, scale);
    }
}
