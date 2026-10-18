using Godot;

namespace LandBuilder.Presentation;

/// <summary>The tree library: sixteen species, each a handful of low-poly primitives with its own silhouette.</summary>
public sealed partial class PropBuilder
{
    /// <summary>
    /// Forests grow in stands: world-space noise picks the locally dominant species (so a birch grove sits inside
    /// the pines and carries on across the tile border), sizes run from saplings to old giants, and the odd glade
    /// is left open with a stump, a log or mushrooms.
    /// </summary>
    private void AddForestTree(Vector3 p, float scale)
    {
        var world = _sampler.ToWorld(p.X, 0, p.Z);
        var glade = Noise.Value(world.X * 2.2f, world.Z * 2.2f, 404);
        if (glade > 0.86f)
        {
            AddUndergrowth(p, scale);
            return;
        }

        var species = StandSpecies(world);
        var size = _rng.Randf() switch
        {
            < 0.14f => _rng.RandfRange(0.55f, 0.72f),
            > 0.9f => _rng.RandfRange(1.3f, 1.55f),
            _ => _rng.RandfRange(0.88f, 1.2f)
        };
        AddTree(species, p, scale * size);
    }

    /// <summary>The species that dominates the stand at a world position, with a few strays mixed in.</summary>
    private TreeSpecies StandSpecies(Vector3 world)
    {
        var options = _style.ForestTrees;
        if (options.Length == 1 || _rng.Randf() < 0.18f)
            return PickWeighted(options);
        var total = 0f;
        foreach (var (_, weight) in options)
            total += weight;
        // Fbm clusters around 0.5; stretch it so every species gets stands of its own.
        var n = Mathf.Clamp((Noise.Fbm(world.X * 0.9f + 31f, world.Z * 0.9f - 17f, 2, 505) - 0.25f) * 2f, 0f, 0.999f) * total;
        foreach (var (species, weight) in options)
        {
            if (n < weight)
                return species;
            n -= weight;
        }

        return options[^1].Species;
    }

    private void AddMeadowTree(Vector3 p, float scale) =>
        AddTree(PickWeighted(_style.MeadowTrees), p, scale);

    private void AddTree(TreeSpecies species, Vector3 p, float scale, bool snowy = false)
    {
        switch (species)
        {
            case TreeSpecies.Pine:
                AddConifer(p, scale, snowy);
                break;
            case TreeSpecies.Fir:
                AddFir(p, scale, snowy);
                break;
            case TreeSpecies.Oak:
                AddRoundTree(p, scale * (_style.Rainforest ? 1.25f : 1f), Pick(_style.Leaves));
                break;
            case TreeSpecies.Birch:
                AddBirch(p, scale);
                break;
            case TreeSpecies.Maple:
                AddMaple(p, scale);
                break;
            case TreeSpecies.Cherry:
                AddCherry(p, scale);
                break;
            case TreeSpecies.Palm:
                AddPalm(p, scale);
                break;
            case TreeSpecies.Acacia:
                AddAcacia(p, scale);
                break;
            case TreeSpecies.Baobab:
                AddBaobab(p, scale);
                break;
            case TreeSpecies.JungleGiant:
                AddJungleGiant(p, scale);
                break;
            case TreeSpecies.Banana:
                AddBanana(p, scale);
                break;
            case TreeSpecies.Bamboo:
                AddBamboo(p, scale);
                break;
            case TreeSpecies.Willow:
                AddWillow(p, scale);
                break;
            case TreeSpecies.BlackPine:
                AddBlackPine(p, scale);
                break;
            case TreeSpecies.Cypress:
                AddCypress(p, scale);
                break;
            case TreeSpecies.Poplar:
                AddPoplar(p, scale);
                break;
        }
    }

    private void Trunk(Vector3 p, float yaw, float scale, float bottom, float top, float height, Color? color = null, int sides = 5)
    {
        At(p, yaw, scale);
        _kit.Sway = Sway.Trunk;
        _kit.Frustum(sides, bottom, top, height, color ?? Palette.TrunkBrown);
    }

    private void AddConifer(Vector3 p, float scale, bool snowy = false)
    {
        var yaw = _rng.Randf() * Mathf.Tau;
        Trunk(p, yaw, scale, 0.024f, 0.018f, 0.06f);
        var green = Vary(Pick(_style.Conifers), _rng);
        float[] radii = [0.105f, 0.083f, 0.058f];
        for (var i = 0; i < radii.Length; i++)
        {
            _kit.Sway = Sway.Evergreen;
            At(p + new Vector3(0, (0.045f + (i * 0.075f)) * scale, 0), yaw + (i * 0.4f), scale);
            var top = snowy ? Palette.Snow : green.Darkened(0.1f);
            _kit.Frustum(7, radii[i], 0.008f, 0.13f, green.Lightened(i * 0.05f), top, capBottom: true, bottom: green.Darkened(0.25f));
            if (snowy)
            {
                At(p + new Vector3(0, (0.12f + (i * 0.075f)) * scale, 0), yaw + (i * 0.4f), scale);
                _kit.Cone(7, radii[i] * 0.45f, 0.055f, Palette.Snow);
            }
        }
    }

    /// <summary>Tall narrow fir: many thin tiers, darker needles.</summary>
    private void AddFir(Vector3 p, float scale, bool snowy = false)
    {
        var yaw = _rng.Randf() * Mathf.Tau;
        Trunk(p, yaw, scale, 0.02f, 0.015f, 0.05f, new Color("6b4a32"));
        var green = Vary(Pick(_style.Conifers), _rng).Darkened(0.08f);
        const int tiers = 5;
        for (var i = 0; i < tiers; i++)
        {
            _kit.Sway = Sway.Evergreen;
            var r = Mathf.Lerp(0.085f, 0.03f, i / (float)(tiers - 1));
            At(p + new Vector3(0, (0.04f + (i * 0.06f)) * scale, 0), yaw + (i * 0.6f), scale);
            _kit.Frustum(6, r, 0.006f, 0.1f, green.Lightened(i * 0.03f), snowy ? Palette.Snow : green, capBottom: true, bottom: green.Darkened(0.25f));
        }
    }

    private void AddRoundTree(Vector3 p, float scale, Color leaf)
    {
        var yaw = _rng.Randf() * Mathf.Tau;
        Trunk(p, yaw, scale, 0.022f, 0.016f, 0.11f);
        _kit.Sway = Sway.Deciduous;
        At(p + new Vector3(0, 0.15f * scale, 0), yaw, scale);
        _kit.Blob(new Vector3(0.095f, 0.09f, 0.095f), Vary(leaf, _rng), _rng.RandiRange(0, 9999));
        if (_rng.Randf() < 0.6f)
        {
            At(p + new Vector3(0.04f, 0.2f, 0.02f) * scale, yaw, scale);
            _kit.Blob(new Vector3(0.06f, 0.055f, 0.06f), Vary(leaf.Lightened(0.08f), _rng), _rng.RandiRange(0, 9999));
        }
    }

    private void AddBirch(Vector3 p, float scale)
    {
        var yaw = _rng.Randf() * Mathf.Tau;
        Trunk(p, yaw, scale, 0.014f, 0.01f, 0.2f, new Color("eeeae0"));
        // Dark bark marks.
        for (var i = 0; i < 3; i++)
        {
            At(p + new Vector3(0, (0.04f + (i * 0.05f)) * scale, 0), yaw + i, scale);
            _kit.Box(new Vector3(0.026f, 0.006f, 0.026f), new Color("3a3a36"));
        }

        var leaf = Pick(_style.Leaves);
        for (var i = 0; i < 3; i++)
        {
            _kit.Sway = Sway.Deciduous;
            At(p + new Vector3(_rng.RandfRange(-0.02f, 0.02f), (0.16f + (i * 0.05f)) * scale, _rng.RandfRange(-0.02f, 0.02f)), yaw, scale);
            _kit.Blob(new Vector3(0.05f, 0.06f, 0.05f) * (1f - (i * 0.2f)), Vary(leaf.Lightened(0.06f), _rng), _rng.RandiRange(0, 9999));
        }
    }

    /// <summary>Maple: wide crown in several lobes, fiery colours in autumn themes.</summary>
    private void AddMaple(Vector3 p, float scale)
    {
        var yaw = _rng.Randf() * Mathf.Tau;
        Trunk(p, yaw, scale, 0.022f, 0.015f, 0.1f, new Color("6b4a32"));
        var leaf = Pick(_style.Leaves);
        for (var i = 0; i < 4; i++)
        {
            _kit.Sway = Sway.Deciduous;
            var a = (i * Mathf.Tau / 4f) + yaw;
            var offset = new Vector3(Mathf.Cos(a) * 0.045f, 0.14f + (i % 2 * 0.03f), Mathf.Sin(a) * 0.045f);
            At(p + (offset * scale), yaw, scale);
            _kit.Blob(new Vector3(0.065f, 0.055f, 0.065f), Vary(leaf, _rng, 0.1f), _rng.RandiRange(0, 9999));
        }
    }

    /// <summary>Old-growth giant that appears deep inside large forests.</summary>
    private void AddAncientTree(Vector3 p, float scale)
    {
        var species = _style.Rainforest ? TreeSpecies.JungleGiant : _style.ConiferHeavy() ? TreeSpecies.Fir : TreeSpecies.Oak;
        AddTree(species, p, scale * 1.75f);
    }

    /// <summary>Cherry: dark crooked trunk and a wide, flat cloud of blossom.</summary>
    private void AddCherry(Vector3 p, float scale)
    {
        Cherries++;
        var yaw = _rng.Randf() * Mathf.Tau;
        var bark = new Color("4a3a32");
        At(p, new Basis(Vector3.Up, yaw) * new Basis(Vector3.Forward, 0.18f) * Basis.Identity.Scaled(Vector3.One * scale));
        _kit.Sway = Sway.Trunk;
        _kit.Frustum(5, 0.02f, 0.014f, 0.1f, bark);
        var top = p + (new Basis(Vector3.Up, yaw) * new Basis(Vector3.Forward, 0.18f) * new Vector3(0, 0.1f, 0) * scale);
        At(top, new Basis(Vector3.Up, yaw + 2f) * new Basis(Vector3.Forward, -0.5f) * Basis.Identity.Scaled(Vector3.One * scale));
        _kit.Frustum(4, 0.01f, 0.006f, 0.06f, bark);
        for (var i = 0; i < 4; i++)
        {
            _kit.Sway = Sway.Flower;
            var a = (i * Mathf.Tau / 4f) + yaw;
            At(top + (new Vector3(Mathf.Cos(a) * 0.05f, 0.03f + (i % 2 * 0.02f), Mathf.Sin(a) * 0.05f) * scale), yaw, scale);
            _kit.Blob(new Vector3(0.07f, 0.04f, 0.07f), Vary(Pick(_style.Blossoms), _rng, 0.05f), _rng.RandiRange(0, 9999));
        }
    }

    private void AddPalm(Vector3 p, float scale)
    {
        var yaw = _rng.Randf() * Mathf.Tau;
        var lean = _rng.RandfRange(0.1f, 0.3f);
        var basis = new Basis(Vector3.Up, yaw) * new Basis(Vector3.Forward, lean);
        _kit.Sway = Sway.Trunk;
        At(p, basis.Scaled(Vector3.One * scale));
        _kit.Frustum(5, 0.016f, 0.011f, 0.24f, new Color("9c7650"));
        var top = p + (basis * new Vector3(0, 0.24f, 0) * scale);
        for (var i = 0; i < 6; i++)
        {
            _kit.Sway = Sway.Evergreen;
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

    private void AddAcacia(Vector3 p, float scale)
    {
        var yaw = _rng.Randf() * Mathf.Tau;
        Trunk(p, yaw, scale, 0.016f, 0.012f, 0.16f, new Color("6b4a32"));
        var basis = new Basis(Vector3.Up, yaw);
        At(p + (basis * new Vector3(0.03f, 0.12f, 0) * scale), new Basis(Vector3.Up, yaw) * new Basis(Vector3.Forward, -0.6f) * Basis.Identity.Scaled(Vector3.One * scale));
        _kit.Frustum(4, 0.009f, 0.006f, 0.07f, new Color("6b4a32"));
        _kit.Sway = Sway.Deciduous;
        At(p + new Vector3(0, 0.17f * scale, 0), yaw, scale);
        var leaf = Pick(_style.Leaves);
        _kit.Frustum(9, 0.13f, 0.11f, 0.035f, Vary(leaf, _rng), Vary(leaf, _rng).Lightened(0.08f), capBottom: true, bottom: leaf.Darkened(0.25f));
    }

    /// <summary>Baobab: a fat bottle trunk with stubby branches and tufts of leaves.</summary>
    private void AddBaobab(Vector3 p, float scale)
    {
        var yaw = _rng.Randf() * Mathf.Tau;
        var bark = new Color("a08a72");
        Trunk(p, yaw, scale, 0.07f, 0.045f, 0.2f, bark, 8);
        for (var i = 0; i < 5; i++)
        {
            var a = yaw + (i * Mathf.Tau / 5f);
            var branch = new Basis(Vector3.Up, a) * new Basis(Vector3.Forward, -0.9f);
            At(p + new Vector3(0, 0.19f * scale, 0), branch.Scaled(Vector3.One * scale));
            _kit.Frustum(4, 0.012f, 0.006f, 0.07f, bark);
            _kit.Sway = Sway.Deciduous;
            At(p + (new Vector3(Mathf.Cos(a) * 0.06f, 0.24f, -Mathf.Sin(a) * 0.06f) * scale), yaw, scale);
            _kit.Blob(new Vector3(0.035f, 0.025f, 0.035f), Vary(Pick(_style.Leaves), _rng), _rng.RandiRange(0, 9999));
            _kit.Sway = Sway.Trunk;
        }
    }

    /// <summary>Rainforest emergent: buttressed trunk, a high umbrella crown and a lower leafy layer.</summary>
    private void AddJungleGiant(Vector3 p, float scale)
    {
        var yaw = _rng.Randf() * Mathf.Tau;
        var bark = new Color("8a7058");
        Trunk(p, yaw, scale, 0.025f, 0.016f, 0.34f, bark, 6);
        for (var i = 0; i < 3; i++)
        {
            var a = yaw + (i * Mathf.Tau / 3f);
            At(p + (new Vector3(Mathf.Cos(a) * 0.02f, 0, -Mathf.Sin(a) * 0.02f) * scale), new Basis(Vector3.Up, a) * Basis.Identity.Scaled(Vector3.One * scale));
            _kit.Box(new Vector3(0.05f, 0.06f, 0.008f), bark.Darkened(0.1f));
        }

        _kit.Sway = Sway.Deciduous;
        var leaf = Pick(_style.Leaves);
        At(p + new Vector3(0, 0.34f * scale, 0), yaw, scale);
        _kit.Blob(new Vector3(0.13f, 0.05f, 0.13f), Vary(leaf, _rng), _rng.RandiRange(0, 9999));
        At(p + new Vector3(0.05f, 0.38f, -0.03f) * scale, yaw, scale);
        _kit.Blob(new Vector3(0.08f, 0.04f, 0.08f), Vary(leaf.Lightened(0.08f), _rng), _rng.RandiRange(0, 9999));
        At(p + new Vector3(-0.03f, 0.2f, 0.02f) * scale, yaw, scale);
        _kit.Blob(new Vector3(0.07f, 0.05f, 0.07f), Vary(leaf.Darkened(0.1f), _rng), _rng.RandiRange(0, 9999));
    }

    /// <summary>Banana plant: short trunk with long arching leaves.</summary>
    private void AddBanana(Vector3 p, float scale)
    {
        var yaw = _rng.Randf() * Mathf.Tau;
        Trunk(p, yaw, scale, 0.014f, 0.01f, 0.08f, new Color("7c8a4a"));
        var leaf = new Color("74b84a");
        _kit.OrientFrom = null;
        for (var i = 0; i < 6; i++)
        {
            _kit.Sway = Sway.Evergreen;
            var a = yaw + (i * Mathf.Tau / 6f);
            var b = new Basis(Vector3.Up, a) * new Basis(Vector3.Right, -0.5f - (_rng.Randf() * 0.3f));
            _kit.Transform = new Transform3D(b.Scaled(Vector3.One * scale), p + new Vector3(0, 0.08f * scale, 0));
            var c = Vary(leaf, _rng);
            _kit.Quad(new Vector3(-0.025f, 0, 0), new Vector3(0.025f, 0, 0), new Vector3(0.02f, -0.02f, 0.14f), new Vector3(-0.02f, -0.02f, 0.14f), c);
            _kit.Quad(new Vector3(0.025f, 0, 0), new Vector3(-0.025f, 0, 0), new Vector3(-0.02f, -0.02f, 0.14f), new Vector3(0.02f, -0.02f, 0.14f), c.Darkened(0.12f));
        }
    }

    /// <summary>Bamboo grove: a clump of tall segmented culms with leafy tops.</summary>
    private void AddBamboo(Vector3 p, float scale)
    {
        var stems = _rng.RandiRange(6, 9);
        for (var i = 0; i < stems; i++)
        {
            var q = p + new Vector3(_rng.RandfRange(-0.04f, 0.04f), 0, _rng.RandfRange(-0.04f, 0.04f));
            q.Y = Ground(q);
            var h = _rng.RandfRange(0.22f, 0.34f) * scale;
            var lean = new Basis(Vector3.Up, _rng.Randf() * Mathf.Tau) * new Basis(Vector3.Right, _rng.RandfRange(0f, 0.12f));
            _kit.Sway = Sway.Grass;
            At(q, lean.Scaled(new Vector3(scale, h / 0.3f, scale)));
            _kit.Frustum(5, 0.007f, 0.006f, 0.3f, new Color("8fb04a"), new Color("6f8f3a"));
            for (var n = 1; n < 4; n++)
            {
                At(q + (lean * new Vector3(0, h * n / 4f, 0)), lean.Scaled(Vector3.One * scale));
                _kit.Frustum(5, 0.009f, 0.009f, 0.004f, new Color("6f8f3a"));
            }

            _kit.Sway = Sway.Evergreen;
            At(q + (lean * new Vector3(0, h, 0)), lean.Scaled(Vector3.One * scale));
            _kit.Blob(new Vector3(0.03f, 0.035f, 0.03f), Vary(new Color("7fb04f"), _rng), _rng.RandiRange(0, 9999));
        }
    }

    /// <summary>Willow: round head with a curtain of drooping branches.</summary>
    private void AddWillow(Vector3 p, float scale)
    {
        var yaw = _rng.Randf() * Mathf.Tau;
        Trunk(p, yaw, scale, 0.02f, 0.015f, 0.13f, new Color("6b5a42"));
        var leaf = new Color("9cbf5a");
        _kit.Sway = Sway.Deciduous;
        At(p + new Vector3(0, 0.16f * scale, 0), yaw, scale);
        _kit.Blob(new Vector3(0.08f, 0.05f, 0.08f), Vary(leaf, _rng), _rng.RandiRange(0, 9999));
        At(p + new Vector3(0, 0.05f * scale, 0), yaw, scale);
        _kit.Frustum(9, 0.1f, 0.075f, 0.12f, Vary(leaf.Darkened(0.06f), _rng));
    }

    /// <summary>Japanese black pine: crooked trunk with flat cloud-like needle pads.</summary>
    private void AddBlackPine(Vector3 p, float scale)
    {
        var yaw = _rng.Randf() * Mathf.Tau;
        var bark = new Color("4f4038");
        var basis1 = new Basis(Vector3.Up, yaw) * new Basis(Vector3.Forward, 0.35f);
        At(p, basis1.Scaled(Vector3.One * scale));
        _kit.Sway = Sway.Trunk;
        _kit.Frustum(5, 0.018f, 0.012f, 0.12f, bark);
        var mid = p + (basis1 * new Vector3(0, 0.12f, 0) * scale);
        var basis2 = new Basis(Vector3.Up, yaw) * new Basis(Vector3.Forward, -0.45f);
        At(mid, basis2.Scaled(Vector3.One * scale));
        _kit.Frustum(5, 0.012f, 0.008f, 0.1f, bark);
        var top = mid + (basis2 * new Vector3(0, 0.1f, 0) * scale);
        var green = Vary(Pick(_style.Conifers), _rng);
        Vector3[] pads = [top, mid + new Vector3(0.05f, 0.01f, 0.02f) * scale, top + new Vector3(-0.06f, -0.03f, 0.03f) * scale];
        foreach (var pad in pads)
        {
            _kit.Sway = Sway.Evergreen;
            At(pad, yaw, scale);
            _kit.Blob(new Vector3(0.06f, 0.022f, 0.05f), green.Lightened(_rng.RandfRange(0f, 0.1f)), _rng.RandiRange(0, 9999), 0.08f);
        }
    }

    private void AddCypress(Vector3 p, float scale)
    {
        var yaw = _rng.Randf() * Mathf.Tau;
        Trunk(p, yaw, scale, 0.012f, 0.01f, 0.04f);
        _kit.Sway = Sway.Evergreen;
        At(p + new Vector3(0, 0.15f * scale, 0), yaw, scale);
        _kit.Blob(new Vector3(0.04f, 0.15f, 0.04f), Vary(Pick(_style.Conifers), _rng), _rng.RandiRange(0, 9999), 0.06f);
    }

    private void AddPoplar(Vector3 p, float scale)
    {
        var yaw = _rng.Randf() * Mathf.Tau;
        Trunk(p, yaw, scale, 0.014f, 0.01f, 0.08f);
        _kit.Sway = Sway.Deciduous;
        At(p + new Vector3(0, 0.2f * scale, 0), yaw, scale);
        _kit.Blob(new Vector3(0.05f, 0.15f, 0.05f), Vary(Pick(_style.Leaves), _rng), _rng.RandiRange(0, 9999), 0.1f);
    }

    // ---------------------------------------------------------------- shrubs and small things

    private void AddUndergrowth(Vector3 p, float scale)
    {
        var roll = _rng.Randf();
        if (_style.Rainforest && roll < 0.45f)
            AddFern(p, scale);
        else if (roll < 0.55f)
            AddBush(p, scale * _rng.RandfRange(0.6f, 1f), Pick(_style.Leaves).Darkened(0.15f));
        else if (roll < 0.72f)
            AddFern(p, scale * 0.8f);
        else if (roll < 0.86f)
            AddMushrooms(p, scale);
        else
            AddLog(p, scale);
    }

    private void AddFern(Vector3 p, float scale)
    {
        _kit.OrientFrom = null;
        var c = new Color("4f9a44");
        for (var i = 0; i < 5; i++)
        {
            _kit.Sway = Sway.Grass;
            var b = new Basis(Vector3.Up, (i * Mathf.Tau / 5f) + _rng.Randf()) * new Basis(Vector3.Right, -0.7f);
            _kit.Transform = new Transform3D(b.Scaled(Vector3.One * scale), p);
            _kit.Triangle(new Vector3(-0.012f, 0, 0), new Vector3(0.012f, 0, 0), new Vector3(0, 0, 0.08f), Vary(c, _rng));
            _kit.Triangle(new Vector3(0.012f, 0, 0), new Vector3(-0.012f, 0, 0), new Vector3(0, 0, 0.08f), c.Darkened(0.15f));
        }
    }

    private void AddBush(Vector3 p, float scale, Color color)
    {
        At(p, _rng.Randf() * Mathf.Tau, scale);
        _kit.Sway = Sway.Bush;
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
        for (var i = 0; i < n; i++)
        {
            _kit.Sway = Sway.Flower;
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
        _kit.Sway = Sway.Rigid;
        At(p + new Vector3(0, -0.01f, 0), _rng.Randf() * Mathf.Tau, scale);
        _kit.Blob(new Vector3(0.045f, 0.03f, 0.038f) * _rng.RandfRange(0.7f, 1.4f), Vary(color, _rng, 0.05f), _rng.RandiRange(0, 9999), 0.25f);
    }

    private void AddMeadowDetail(Vector3 p, float scale)
    {
        var roll = _rng.Randf();
        if (roll < 0.36f)
            AddFlowers(p, scale);
        else if (roll < 0.54f)
            AddBush(p, scale, Pick(_style.Leaves).Lightened(0.1f));
        else if (roll < 0.74f)
            AddMeadowTree(p, scale);
        else if (roll < 0.9f && _style.Seasons.Snow < 0.5f)
            AddLivestock(p, scale);
        else
            AddRock(p, scale * 0.7f, Palette.Stone);
    }

    private void AddVillageGreen(Vector3 p, float scale)
    {
        var roll = _rng.Randf();
        if (roll < 0.45f)
            AddTree(PickWeighted(_style.VillageTrees), p, scale * 0.85f);
        else if (roll < 0.7f)
            AddBush(p, scale, Pick(_style.Leaves));
        else
            AddVillageProp(p, scale);
    }

    private void AddDeadTree(Vector3 p, float scale)
    {
        var yaw = _rng.Randf() * Mathf.Tau;
        var bark = new Color("6e6252");
        Trunk(p, yaw, scale, 0.018f, 0.008f, 0.2f, bark);
        for (var i = 0; i < 3; i++)
        {
            var branchYaw = yaw + (i * 2.1f);
            var basis = new Basis(Vector3.Up, branchYaw) * new Basis(Vector3.Forward, 0.9f + (i * 0.15f));
            At(p + new Vector3(0, (0.08f + (i * 0.04f)) * scale, 0), basis.Scaled(Vector3.One * scale));
            _kit.Frustum(4, 0.007f, 0.002f, 0.07f, bark);
        }
    }
}
