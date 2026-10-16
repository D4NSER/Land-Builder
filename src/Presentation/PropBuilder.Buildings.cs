using Godot;

namespace LandBuilder.Presentation;

/// <summary>
/// Architecture sets. Each theme builds its villages in one style (European half-timber, Nordic turf-roofed cabins,
/// Japanese minka, tropical stilt huts, African rondavels, rustic farmsteads) with matching landmarks.
/// </summary>
public sealed partial class PropBuilder
{
    private static readonly Color Timber = new("5a3e2b");
    private static readonly Color Thatch = new("d2b26a");
    private static readonly Color Vermilion = new("c4452f");

    private Color Wall() => Pick(_style.Walls);

    private Color Roof() => Vary(Pick(_style.Roofs), _rng, 0.06f);

    /// <summary>Faces roughly towards the tile centre with a little jitter, like lanes radiating out.</summary>
    private float HouseYaw(Vector3 p) =>
        Mathf.Atan2(p.X, p.Z) + (Mathf.Round(_rng.RandfRange(-1f, 1f)) * Mathf.Pi / 2f) + _rng.RandfRange(-0.15f, 0.15f);

    private Vector3 Local(Vector3 p, float yaw, Vector3 offset, float scale) => p + (new Basis(Vector3.Up, yaw) * offset * scale);

    private void AddHouse(Vector3 p, float scale)
    {
        _kit.Sway = Sway.Building;
        switch (_style.Architecture)
        {
            case Architecture.Nordic:
                AddNordicCabin(p, scale);
                break;
            case Architecture.Japanese:
                AddMinka(p, scale);
                break;
            case Architecture.Tropical:
                AddStiltHut(p, scale);
                break;
            case Architecture.African:
                AddRondavel(p, scale, _rng.RandfRange(0.9f, 1.15f));
                break;
            case Architecture.Rustic:
                if (_rng.Randf() < 0.3f)
                    AddBarn(p, scale * 0.8f);
                else
                    AddEuropeanHouse(p, scale, halfTimber: _rng.Randf() < 0.5f);
                break;
            default:
                if (_rng.Randf() < 0.3f)
                    AddTownhouse(p, scale);
                else
                    AddEuropeanHouse(p, scale, halfTimber: _rng.Randf() < 0.55f);
                break;
        }
    }

    // ---------------------------------------------------------------- European

    private void AddEuropeanHouse(Vector3 p, float scale, bool halfTimber)
    {
        var yaw = HouseYaw(p);
        var w = _rng.RandfRange(0.11f, 0.15f);
        var d = _rng.RandfRange(0.085f, 0.105f);
        var h = _rng.RandfRange(0.07f, 0.1f) * StoreyFactor();
        var wall = Wall();
        At(p + new Vector3(0, -0.01f, 0), yaw, scale);
        _kit.Box(new Vector3(w, h + 0.01f, d), wall);
        if (halfTimber)
            TimberFrame(p, yaw, scale, w, d, h);
        At(p + new Vector3(0, h, 0), yaw, scale);
        _kit.GableRoof(w * 1.12f, d * 1.22f, 0.065f, Roof(), wall.Darkened(0.06f));
        if (_rng.Randf() < 0.6f)
        {
            var chimney = Local(p, yaw, new Vector3(w * 0.28f, h + 0.03f, d * 0.18f), scale);
            At(chimney, yaw, scale);
            _kit.Box(new Vector3(0.018f, 0.05f, 0.018f), Palette.StoneDark);
            Chimneys.Add(chimney + new Vector3(0, 0.05f * scale, 0));
        }

        Door(p, yaw, scale, d);
        Window(p, yaw, scale, new Vector3(w * 0.3f, h * 0.45f, d * 0.5f));
        Window(p, yaw, scale, new Vector3(-w * 0.3f, h * 0.45f, d * 0.5f));
    }

    /// <summary>Tall narrow two-storey house with a steep roof and a dormer.</summary>
    private void AddTownhouse(Vector3 p, float scale)
    {
        var yaw = HouseYaw(p);
        const float w = 0.09f;
        const float d = 0.09f;
        var h = _rng.RandfRange(0.13f, 0.16f) * (1f + (_context.VillageTier * 0.35f));
        var wall = Wall();
        At(p + new Vector3(0, -0.01f, 0), yaw, scale);
        _kit.Box(new Vector3(w, h + 0.01f, d), wall);
        At(p + new Vector3(0, h * 0.5f, 0), yaw, scale);
        _kit.Box(new Vector3(w + 0.004f, 0.006f, d + 0.004f), wall.Darkened(0.15f));
        At(p + new Vector3(0, h, 0), yaw, scale);
        var roof = Roof();
        _kit.GableRoof(w * 1.15f, d * 1.2f, 0.08f, roof, wall.Darkened(0.06f));
        At(Local(p, yaw, new Vector3(0, h + 0.01f, d * 0.35f), scale), yaw + (Mathf.Pi / 2f), scale);
        _kit.Box(new Vector3(0.025f, 0.03f, 0.025f), wall);
        At(Local(p, yaw, new Vector3(0, h + 0.04f, d * 0.35f), scale), yaw + (Mathf.Pi / 2f), scale);
        _kit.GableRoof(0.034f, 0.034f, 0.018f, roof, wall);
        Door(p, yaw, scale, d);
        Window(p, yaw, scale, new Vector3(0, h * 0.72f, d * 0.5f));
    }

    private void TimberFrame(Vector3 p, float yaw, float scale, float w, float d, float h)
    {
        foreach (var sx in new[] { -1f, 1f })
        {
            foreach (var sz in new[] { -1f, 1f })
            {
                At(Local(p, yaw, new Vector3(sx * w * 0.5f, -0.01f, sz * d * 0.5f), scale), yaw, scale);
                _kit.Box(new Vector3(0.009f, h + 0.01f, 0.009f), Timber);
            }
        }

        At(Local(p, yaw, new Vector3(0, h * 0.5f, 0), scale), yaw, scale);
        _kit.Box(new Vector3(w + 0.004f, 0.007f, d + 0.004f), Timber);
        // Diagonal braces on the front face.
        foreach (var sx in new[] { -1f, 1f })
        {
            var brace = new Basis(Vector3.Up, yaw) * new Basis(Vector3.Back, sx * 0.7f);
            At(Local(p, yaw, new Vector3(sx * w * 0.32f, h * 0.55f, d * 0.5f + 0.002f), scale), brace.Scaled(Vector3.One * scale));
            _kit.Box(new Vector3(0.006f, h * 0.42f, 0.003f), Timber);
        }
    }

    private void Door(Vector3 p, float yaw, float scale, float depth)
    {
        At(Local(p, yaw, new Vector3(0, 0, (depth * 0.5f) + 0.001f), scale), yaw, scale);
        _kit.Box(new Vector3(0.022f, 0.04f, 0.003f), new Color("6b4a32"));
    }

    private void Window(Vector3 p, float yaw, float scale, Vector3 offset)
    {
        At(Local(p, yaw, offset + new Vector3(0, 0, 0.001f), scale), yaw, scale);
        _kit.Box(new Vector3(0.02f, 0.02f, 0.003f), new Color("ffe7a3"));
    }

    private void AddChurch(Vector3 p, float scale)
    {
        var yaw = _rng.Randf() * Mathf.Tau;
        var wall = Wall();
        var roof = Roof();
        At(p + new Vector3(0, -0.01f, 0), yaw, scale);
        _kit.Box(new Vector3(0.22f, 0.13f, 0.13f), wall);
        At(p + new Vector3(0, 0.12f, 0), yaw, scale);
        _kit.GableRoof(0.24f, 0.16f, 0.08f, roof, wall.Darkened(0.05f));
        var tower = Local(p, yaw, new Vector3(-0.13f, 0, 0), scale);
        At(tower + new Vector3(0, -0.01f, 0), yaw, scale);
        _kit.Box(new Vector3(0.08f, 0.27f, 0.08f), wall);
        At(tower + new Vector3(0, 0.26f * scale, 0), yaw + (Mathf.Pi / 4f), scale);
        _kit.Frustum(4, 0.065f, 0f, 0.16f, roof);
        At(Local(tower, yaw, new Vector3(0, 0.19f, 0.041f), scale), yaw, scale);
        _kit.Box(new Vector3(0.03f, 0.04f, 0.003f), Palette.Gold);
    }

    // ---------------------------------------------------------------- Nordic

    /// <summary>Falu-red log cabin with white corner boards and a grassy turf roof.</summary>
    private void AddNordicCabin(Vector3 p, float scale)
    {
        var yaw = HouseYaw(p);
        var w = _rng.RandfRange(0.12f, 0.15f);
        var d = _rng.RandfRange(0.085f, 0.1f);
        var h = _rng.RandfRange(0.065f, 0.08f) * StoreyFactor();
        var wall = Wall();
        At(p + new Vector3(0, -0.01f, 0), yaw, scale);
        _kit.Box(new Vector3(w, h + 0.01f, d), wall);
        foreach (var sx in new[] { -1f, 1f })
        {
            foreach (var sz in new[] { -1f, 1f })
            {
                At(Local(p, yaw, new Vector3(sx * w * 0.5f, -0.01f, sz * d * 0.5f), scale), yaw, scale);
                _kit.Box(new Vector3(0.01f, h + 0.01f, 0.01f), new Color("f4f1ea"));
            }
        }

        At(p + new Vector3(0, h, 0), yaw, scale);
        var turf = Roof();
        _kit.GableRoof(w * 1.15f, d * 1.3f, 0.06f, turf, wall.Darkened(0.1f));
        Door(p, yaw, scale, d);
        Window(p, yaw, scale, new Vector3(w * 0.3f, h * 0.5f, d * 0.5f));
    }

    /// <summary>Stave church: stacked dark shingle roofs and a tall spire.</summary>
    private void AddStaveChurch(Vector3 p, float scale)
    {
        var yaw = _rng.Randf() * Mathf.Tau;
        var wood = new Color("5a3a2a");
        var shingle = new Color("3a2f28");
        At(p + new Vector3(0, -0.01f, 0), yaw, scale);
        _kit.Box(new Vector3(0.17f, 0.08f, 0.17f), wood);
        At(p + new Vector3(0, 0.07f * scale, 0), yaw, scale);
        _kit.HipRoof(0.22f, 0.22f, 0.05f, shingle);
        At(p + new Vector3(0, 0.1f * scale, 0), yaw, scale);
        _kit.Box(new Vector3(0.11f, 0.06f, 0.11f), wood);
        At(p + new Vector3(0, 0.155f * scale, 0), yaw, scale);
        _kit.HipRoof(0.15f, 0.15f, 0.04f, shingle);
        At(p + new Vector3(0, 0.18f * scale, 0), yaw, scale);
        _kit.Box(new Vector3(0.06f, 0.05f, 0.06f), wood);
        At(p + new Vector3(0, 0.225f * scale, 0), yaw + (Mathf.Pi / 4f), scale);
        _kit.Frustum(4, 0.05f, 0f, 0.14f, shingle);
    }

    // ---------------------------------------------------------------- Japanese

    /// <summary>Minka: white walls in a dark timber frame under a broad hipped tile roof, with a wooden veranda.</summary>
    private void AddMinka(Vector3 p, float scale)
    {
        var yaw = HouseYaw(p);
        var w = _rng.RandfRange(0.12f, 0.15f);
        var d = _rng.RandfRange(0.09f, 0.11f);
        var h = 0.065f * StoreyFactor();
        var wall = Wall();
        At(p + new Vector3(0, -0.01f, 0), yaw, scale);
        _kit.Box(new Vector3(w + 0.02f, 0.016f, d + 0.02f), new Color("8f8a80"));
        At(p + new Vector3(0, 0.005f, 0), yaw, scale);
        _kit.Box(new Vector3(w, h, d), wall);
        foreach (var sx in new[] { -0.5f, 0f, 0.5f })
        {
            foreach (var sz in new[] { -1f, 1f })
            {
                At(Local(p, yaw, new Vector3(sx * w, 0.005f, sz * d * 0.5f), scale), yaw, scale);
                _kit.Box(new Vector3(0.008f, h, 0.008f), Timber.Darkened(0.2f));
            }
        }

        At(p + new Vector3(0, h * 0.55f, 0), yaw, scale);
        _kit.Box(new Vector3(w + 0.003f, 0.006f, d + 0.003f), Timber.Darkened(0.2f));
        // Engawa veranda.
        At(Local(p, yaw, new Vector3(0, 0.004f, d * 0.5f + 0.012f), scale), yaw, scale);
        _kit.Box(new Vector3(w, 0.008f, 0.024f), new Color("a07a50"));
        var roof = Roof();
        At(p + new Vector3(0, h, 0), yaw, scale);
        _kit.HipRoof(w * 1.45f, d * 1.6f, 0.055f, roof);
        // Small gable on top (irimoya style).
        At(p + new Vector3(0, h + 0.035f, 0), yaw, scale);
        _kit.GableRoof(w * 0.55f, d * 0.55f, 0.03f, roof.Darkened(0.05f), roof.Darkened(0.2f));
    }

    /// <summary>Five-storey pagoda with flared tiers and a bronze spire.</summary>
    private void AddPagoda(Vector3 p, float scale)
    {
        var yaw = _rng.Randf() * Mathf.Tau;
        var roof = new Color("3d4757");
        At(p + new Vector3(0, -0.01f, 0), yaw, scale);
        _kit.Box(new Vector3(0.16f, 0.02f, 0.16f), new Color("8f8a80"));
        var y = 0.01f;
        for (var i = 0; i < 5; i++)
        {
            var s = 0.11f - (i * 0.014f);
            At(p + new Vector3(0, y * scale, 0), yaw, scale);
            _kit.Box(new Vector3(s, 0.045f, s), Vermilion);
            y += 0.045f;
            At(p + new Vector3(0, y * scale, 0), yaw, scale);
            _kit.HipRoof(s * 1.9f, s * 1.9f, 0.03f, roof);
            y += 0.012f;
        }

        At(p + new Vector3(0, y * scale, 0), yaw, scale);
        _kit.Frustum(6, 0.008f, 0.004f, 0.12f, new Color("b08a3a"));
        for (var r = 0; r < 4; r++)
        {
            At(p + new Vector3(0, (y + 0.03f + (r * 0.018f)) * scale, 0), yaw, scale);
            _kit.Frustum(8, 0.016f, 0.016f, 0.004f, new Color("b08a3a"));
        }
    }

    private void AddTorii(Vector3 p, float scale)
    {
        var yaw = _rng.Randf() * Mathf.Tau;
        foreach (var sx in new[] { -1f, 1f })
        {
            At(Local(p, yaw, new Vector3(sx * 0.05f, 0, 0), scale), yaw, scale);
            _kit.Frustum(8, 0.008f, 0.007f, 0.12f, Vermilion);
        }

        At(Local(p, yaw, new Vector3(0, 0.095f, 0), scale), yaw, scale);
        _kit.Box(new Vector3(0.12f, 0.01f, 0.012f), Vermilion);
        At(Local(p, yaw, new Vector3(0, 0.118f, 0), scale), yaw, scale);
        _kit.Box(new Vector3(0.15f, 0.012f, 0.016f), new Color("2b2b2b"));
    }

    private void AddStoneLantern(Vector3 p, float scale)
    {
        var stone = new Color("a8a39a");
        At(p, 0, scale);
        _kit.Frustum(6, 0.012f, 0.01f, 0.025f, stone);
        At(p + new Vector3(0, 0.025f * scale, 0), 0, scale);
        _kit.Box(new Vector3(0.022f, 0.016f, 0.022f), new Color("ffe7a3"));
        At(p + new Vector3(0, 0.041f * scale, 0), 0, scale);
        _kit.HipRoof(0.04f, 0.04f, 0.016f, stone);
    }

    private void AddKura(Vector3 p, float scale)
    {
        var yaw = _rng.Randf() * Mathf.Tau;
        At(p + new Vector3(0, -0.01f, 0), yaw, scale);
        _kit.Box(new Vector3(0.1f, 0.1f, 0.08f), new Color("f2eee6"));
        At(p + new Vector3(0, -0.01f, 0), yaw, scale);
        _kit.Box(new Vector3(0.102f, 0.03f, 0.082f), new Color("4a4a4a"));
        At(p + new Vector3(0, 0.09f, 0), yaw, scale);
        _kit.GableRoof(0.13f, 0.11f, 0.05f, new Color("3d4757"), new Color("f2eee6"));
    }

    // ---------------------------------------------------------------- Tropical

    /// <summary>Stilt hut: raised wooden floor, woven walls and a steep thatched hip roof.</summary>
    private void AddStiltHut(Vector3 p, float scale)
    {
        var yaw = HouseYaw(p);
        var w = _rng.RandfRange(0.1f, 0.13f);
        var d = _rng.RandfRange(0.08f, 0.1f);
        const float legs = 0.04f;
        foreach (var sx in new[] { -1f, 1f })
        {
            foreach (var sz in new[] { -1f, 1f })
            {
                At(Local(p, yaw, new Vector3(sx * w * 0.42f, -0.01f, sz * d * 0.42f), scale), yaw, scale);
                _kit.Box(new Vector3(0.008f, legs + 0.01f, 0.008f), Timber);
            }
        }

        At(p + new Vector3(0, legs * scale, 0), yaw, scale);
        _kit.Box(new Vector3(w + 0.02f, 0.008f, d + 0.02f), new Color("a07a50"));
        At(p + new Vector3(0, (legs + 0.008f) * scale, 0), yaw, scale);
        _kit.Box(new Vector3(w, 0.05f, d), Wall());
        At(p + new Vector3(0, (legs + 0.058f) * scale, 0), yaw, scale);
        _kit.HipRoof(w * 1.4f, d * 1.5f, 0.08f, Vary(Thatch, _rng, 0.05f));
        // Ladder.
        At(Local(p, yaw, new Vector3(0, 0, d * 0.5f + 0.02f), scale), new Basis(Vector3.Up, yaw) * new Basis(Vector3.Right, -0.4f) * Basis.Identity.Scaled(Vector3.One * scale));
        _kit.Box(new Vector3(0.02f, 0.055f, 0.004f), Timber);
    }

    private void AddLonghouse(Vector3 p, float scale)
    {
        var yaw = _rng.Randf() * Mathf.Tau;
        for (var i = -2; i <= 2; i++)
        {
            foreach (var sz in new[] { -1f, 1f })
            {
                At(Local(p, yaw, new Vector3(i * 0.05f, -0.01f, sz * 0.045f), scale), yaw, scale);
                _kit.Box(new Vector3(0.008f, 0.05f, 0.008f), Timber);
            }
        }

        At(p + new Vector3(0, 0.04f * scale, 0), yaw, scale);
        _kit.Box(new Vector3(0.24f, 0.05f, 0.1f), Wall());
        At(p + new Vector3(0, 0.09f * scale, 0), yaw, scale);
        _kit.HipRoof(0.3f, 0.15f, 0.1f, Thatch);
    }

    // ---------------------------------------------------------------- African

    /// <summary>Rondavel: round earthen walls under a conical thatch roof.</summary>
    private void AddRondavel(Vector3 p, float scale, float size)
    {
        var yaw = _rng.Randf() * Mathf.Tau;
        var s = scale * size;
        At(p + new Vector3(0, -0.01f, 0), yaw, s);
        _kit.Frustum(10, 0.05f, 0.048f, 0.065f, Wall());
        At(p + new Vector3(0, 0.05f * s, 0), yaw, s);
        _kit.Frustum(10, 0.072f, 0f, 0.075f, Vary(Thatch, _rng, 0.06f), capBottom: true, bottom: Thatch.Darkened(0.3f));
        At(Local(p, yaw, new Vector3(0, 0, 0.05f), s), yaw, s);
        _kit.Box(new Vector3(0.02f, 0.035f, 0.004f), new Color("3a2a20"));
    }

    private void AddGranary(Vector3 p, float scale)
    {
        foreach (var a in new[] { 0f, 2.1f, 4.2f })
        {
            At(p + (new Vector3(Mathf.Cos(a) * 0.022f, -0.01f, Mathf.Sin(a) * 0.022f) * scale), 0, scale);
            _kit.Box(new Vector3(0.006f, 0.035f, 0.006f), Timber);
        }

        At(p + new Vector3(0, 0.025f * scale, 0), 0, scale);
        _kit.Frustum(8, 0.03f, 0.035f, 0.04f, Wall());
        At(p + new Vector3(0, 0.063f * scale, 0), 0, scale);
        _kit.Cone(8, 0.045f, 0.045f, Thatch);
    }

    private void AddKraal(Vector3 p, float scale)
    {
        AddRondavel(p, scale, 1.35f);
        for (var i = 0; i < 3; i++)
        {
            var a = (i * Mathf.Tau / 3f) + 0.5f;
            var q = p + (new Vector3(Mathf.Cos(a) * 0.12f, 0, Mathf.Sin(a) * 0.12f) * scale);
            q.Y = Ground(q);
            AddRondavel(q, scale, 0.75f);
        }

        for (var i = 0; i < 18; i++)
        {
            var a = i * Mathf.Tau / 18f;
            var q = p + (new Vector3(Mathf.Cos(a) * 0.2f, 0, Mathf.Sin(a) * 0.2f) * scale);
            q.Y = Ground(q);
            At(q, a, scale);
            _kit.Box(new Vector3(0.006f, 0.03f, 0.006f), Timber);
        }
    }

    // ---------------------------------------------------------------- Rustic farmsteads

    /// <summary>Red barn with a gambrel roof.</summary>
    private void AddBarn(Vector3 p, float scale)
    {
        var yaw = HouseYaw(p);
        var red = new Color("a8452f");
        At(p + new Vector3(0, -0.01f, 0), yaw, scale);
        _kit.Box(new Vector3(0.16f, 0.09f, 0.11f), red);
        At(p + new Vector3(0, 0.08f, 0), yaw, scale);
        _kit.GableRoof(0.17f, 0.13f, 0.035f, new Color("4a4a4a"), red);
        At(p + new Vector3(0, 0.11f * scale, 0), yaw, scale);
        _kit.GableRoof(0.17f, 0.085f, 0.04f, new Color("4a4a4a"), red);
        At(Local(p, yaw, new Vector3(0, 0, 0.056f), scale), yaw, scale);
        _kit.Box(new Vector3(0.05f, 0.06f, 0.003f), new Color("f4f1ea"));
    }

    private void AddSilo(Vector3 p, float scale)
    {
        At(p + new Vector3(0, -0.01f, 0), 0, scale);
        _kit.Frustum(10, 0.04f, 0.04f, 0.2f, new Color("c9c4bb"));
        At(p + new Vector3(0, 0.19f * scale, 0), 0, scale);
        _kit.Blob(new Vector3(0.042f, 0.03f, 0.042f), new Color("8a8f96"), 7, 0.02f);
    }

    // ---------------------------------------------------------------- landmarks & props

    private void AddVillageLandmark(Vector3 p, float scale)
    {
        _kit.Sway = Sway.Building;
        switch (_style.Architecture)
        {
            case Architecture.Nordic:
                AddStaveChurch(p, scale);
                break;
            case Architecture.Japanese:
                AddPagoda(p, scale);
                break;
            case Architecture.Tropical:
                AddLonghouse(p, scale);
                break;
            case Architecture.African:
                AddKraal(p, scale * 0.9f);
                break;
            default:
                AddChurch(p, scale);
                break;
        }
    }

    private void AddVillageSmallLandmark(Vector3 p, float scale)
    {
        _kit.Sway = Sway.Rigid;
        switch (_style.Architecture)
        {
            case Architecture.Japanese:
                AddStoneLantern(p, scale * 1.4f);
                break;
            case Architecture.African:
                AddGranary(p, scale);
                break;
            default:
                AddWell(p, scale);
                break;
        }
    }

    private void AddVillageProp(Vector3 p, float scale)
    {
        _kit.Sway = Sway.Rigid;
        switch (_style.Architecture)
        {
            case Architecture.Japanese:
                AddStoneLantern(p, scale);
                break;
            case Architecture.African:
                AddGranary(p, scale * 0.8f);
                break;
            case Architecture.Nordic:
                AddWoodpile(p, scale);
                break;
            case Architecture.Tropical:
                AddCanoe(p, scale);
                break;
            default:
                AddFence(p, scale);
                break;
        }
    }

    private void AddFarmLandmark(Vector3 p, float scale)
    {
        _kit.Sway = Sway.Building;
        switch (_style.Architecture)
        {
            case Architecture.Rustic:
                AddBarn(p, scale);
                AddSilo(Local(p, 0f, new Vector3(0.12f, 0, 0), scale) with { Y = Ground(Local(p, 0f, new Vector3(0.12f, 0, 0), scale)) }, scale);
                break;
            case Architecture.Japanese:
                AddKura(p, scale);
                break;
            case Architecture.African:
                AddGranary(p, scale * 1.3f);
                break;
            case Architecture.Tropical:
                AddStiltHut(p, scale);
                break;
            default:
                AddWindmill(p, scale);
                break;
        }
    }

    /// <summary>Market square for towns: a fountain ringed by stalls with striped awnings.</summary>
    private void AddMarket(Vector3 p, float scale)
    {
        _kit.Sway = Sway.Building;
        At(p + new Vector3(0, 0.002f, 0), 0, scale);
        _kit.Frustum(12, 0.11f, 0.11f, 0.006f, new Color("d9cdb0"), new Color("d9cdb0"));
        At(p, 0, scale);
        _kit.Frustum(10, 0.035f, 0.035f, 0.03f, Palette.Stone, new Color("6fc6d9"));
        At(p + new Vector3(0, 0.03f * scale, 0), 0, scale);
        _kit.Frustum(6, 0.008f, 0.006f, 0.05f, Palette.Stone);
        Color[] awnings = [new("d0563f"), new("3d9ad1"), new("e8b23a"), new("6cc070")];
        for (var i = 0; i < 4; i++)
        {
            var a = (i * Mathf.Tau / 4f) + 0.4f;
            var stall = p + (new Vector3(Mathf.Cos(a) * 0.085f, 0, Mathf.Sin(a) * 0.085f) * scale);
            stall.Y = Ground(stall);
            At(stall, -a, scale);
            _kit.Box(new Vector3(0.035f, 0.02f, 0.025f), new Color("a07a50"));
            At(stall + new Vector3(0, 0.035f * scale, 0), -a, scale);
            _kit.GableRoof(0.045f, 0.035f, 0.014f, awnings[i], Colors.White);
        }
    }

    private void AddWell(Vector3 p, float scale)
    {
        At(p, 0, scale);
        _kit.Frustum(8, 0.04f, 0.04f, 0.035f, Palette.Stone, new Color("3a6b8a"));
        foreach (var side in new[] { -1f, 1f })
        {
            At(p + new Vector3(side * 0.032f, 0, 0) * scale, 0, scale);
            _kit.Box(new Vector3(0.008f, 0.09f, 0.008f), Palette.TrunkBrown);
        }

        At(p + new Vector3(0, 0.085f * scale, 0), 0, scale);
        _kit.GableRoof(0.09f, 0.07f, 0.035f, Roof(), Palette.TrunkBrown);
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

    private void AddWoodpile(Vector3 p, float scale)
    {
        var yaw = _rng.Randf() * Mathf.Tau;
        for (var row = 0; row < 3; row++)
        {
            for (var i = 0; i < 3 - row; i++)
            {
                var basis = new Basis(Vector3.Up, yaw) * new Basis(Vector3.Forward, Mathf.Pi / 2f);
                At(Local(p, yaw, new Vector3(-0.03f, 0.01f + (row * 0.016f), (i - ((2 - row) * 0.5f)) * 0.018f), scale), basis.Scaled(Vector3.One * scale));
                _kit.Frustum(6, 0.008f, 0.008f, 0.06f, new Color("8b6a4a"), new Color("d2b48a"), capBottom: true, bottom: new Color("d2b48a"));
            }
        }
    }

    private void AddCanoe(Vector3 p, float scale)
    {
        var yaw = _rng.Randf() * Mathf.Tau;
        At(p + new Vector3(0, 0.005f, 0), yaw, scale);
        _kit.Blob(new Vector3(0.07f, 0.012f, 0.016f), new Color("8a5a3b"), 11, 0.02f);
    }

    private void AddWindmill(Vector3 p, float scale)
    {
        var yaw = _rng.Randf() * Mathf.Tau;
        var wall = Wall();
        At(p, yaw, scale);
        _kit.Frustum(8, 0.075f, 0.05f, 0.32f, wall, wall.Darkened(0.1f));
        At(p + new Vector3(0, 0.32f, 0), yaw, scale);
        _kit.Cone(8, 0.065f, 0.08f, Roof());
        At(p, yaw, scale);
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

        AddPart(AnimatedPartKind.WindmillBlades, "WindmillBlades", bladesKit,
            new Transform3D(new Basis(Vector3.Up, yaw).Scaled(Vector3.One * scale), p + (new Basis(Vector3.Up, yaw) * hub * scale)));
    }

    private void AddPart(AnimatedPartKind kind, string name, MeshKit kit, Transform3D transform)
    {
        var node = new Node3D { Name = name };
        node.AddChild(new MeshInstance3D { Mesh = kit.Commit(withCustom: true), CastShadow = GeometryInstance3D.ShadowCastingSetting.On });
        node.Transform = transform;
        _partsRoot.AddChild(node);
        _parts.Add(new AnimatedPart(kind, node, _rng.Randf() * Mathf.Tau, node.Position));
    }

    /// <summary>Water mill on a river bank: the house faces the channel and its paddle wheel turns in the current.</summary>
    private void TryWaterMill()
    {
        for (var attempt = 0; attempt < 60; attempt++)
        {
            var p = new Vector3(_rng.RandfRange(-0.6f, 0.6f), 0, _rng.RandfRange(-0.6f, 0.6f));
            if (HexLayout.DistanceToHexEdge(p.X, p.Z) < 0.2f || !IsDry(p.X, p.Z))
                continue;

            // Find the direction towards the river.
            var best = -1f;
            var dir = Vector3.Zero;
            for (var i = 0; i < 12; i++)
            {
                var d = HexLayout.AngleToDirection(i * Mathf.Tau / 12f);
                var c = _sampler.ChannelFactor(p.X + (d.X * 0.12f), p.Z + (d.Z * 0.12f));
                if (c > best)
                {
                    best = c;
                    dir = d;
                }
            }

            if (best < 0.7f || _occupied.Exists(o => o.DistanceTo(p) < 0.12f))
                continue;

            p.Y = Ground(p);
            Claim(p, 0.1f);
            Begin(p);
            _kit.Sway = Sway.Rigid;
            var yaw = Mathf.Atan2(-dir.Z, dir.X);
            At(p + new Vector3(0, -0.01f, 0), yaw, 1f);
            _kit.Box(new Vector3(0.1f, 0.04f, 0.09f), Palette.Stone);
            At(p + new Vector3(0, 0.03f, 0), yaw, 1f);
            _kit.Box(new Vector3(0.1f, 0.05f, 0.09f), new Color("a07a50"));
            At(p + new Vector3(0, 0.08f, 0), yaw + (Mathf.Pi / 2f), 1f);
            _kit.GableRoof(0.11f, 0.12f, 0.05f, Roof(), new Color("a07a50"));

            var wheel = new MeshKit { Custom = new Color(0, 0, 0, 0) };
            for (var i = 0; i < 8; i++)
            {
                wheel.Transform = new Transform3D(new Basis(Vector3.Right, i * Mathf.Pi / 4f), Vector3.Zero);
                wheel.Box(new Vector3(0.022f, 0.06f, 0.006f), new Color("7b5537"));
            }

            wheel.Transform = new Transform3D(new Basis(Vector3.Back, Mathf.Pi / 2f), new Vector3(-0.015f, 0, 0));
            wheel.Frustum(8, 0.012f, 0.012f, 0.03f, new Color("4a3a2a"));
            var centre = p + (dir * 0.075f);
            centre.Y = TerrainSampler.WaterLevel + 0.035f;
            AddPart(AnimatedPartKind.WaterWheel, "WaterWheel", wheel, new Transform3D(new Basis(Vector3.Up, yaw), centre));
            return;
        }
    }
}
