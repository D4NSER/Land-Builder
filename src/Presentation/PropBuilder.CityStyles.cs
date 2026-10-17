using Godot;
using LandBuilder.Domain;

namespace LandBuilder.Presentation;

/// <summary>
/// Regional flavour for growing cities: each architecture crowns its Capital with its own landmark, and roofs,
/// towers and open spaces follow the local style, so two Metropolises on different islands never look alike.
/// </summary>
public sealed partial class PropBuilder
{
    /// <summary>The Capital's signature building, in the island's own style.</summary>
    private void AddCapitalLandmark(Vector3 p)
    {
        _kit.Sway = Sway.Building;
        switch (_style.Architecture)
        {
            case Architecture.Japanese:
                AddCastleKeep(p);
                break;
            case Architecture.Nordic:
            case Architecture.Rustic:
                AddClockTower(p);
                break;
            case Architecture.Tropical:
                AddDomedTower(p);
                break;
            case Architecture.African:
                AddAdobeTower(p);
                break;
            default:
                AddSpireTower(p);
                break;
        }
    }

    /// <summary>A Japanese castle keep: battered stone base, five white storeys under dark flared roofs.</summary>
    private void AddCastleKeep(Vector3 p)
    {
        var stone = new Color("9c958a");
        var plaster = new Color("f4f1ea");
        var roof = new Color("3d4650");
        At(p + new Vector3(0, -0.02f, 0), 0.3f, 1f);
        _kit.Frustum(4, 0.2f, 0.15f, 0.14f, stone, stone.Lightened(0.1f), twist: Mathf.Pi / 4f);
        var y = 0.12f;
        for (var i = 0; i < 5; i++)
        {
            var w = 0.2f - (i * 0.03f);
            var h = 0.075f;
            At(p + new Vector3(0, y, 0), 0.3f, 1f);
            _kit.Box(new Vector3(w, h, w * 0.85f), plaster);
            _kit.Sway = Sway.Roof;
            At(p + new Vector3(0, y + h - 0.01f, 0), 0.3f, 1f);
            _kit.HipRoof(w * 1.35f, w * 1.15f, 0.045f, roof, roof.Darkened(0.2f));
            _kit.Sway = Sway.Building;
            y += h + 0.03f;
        }

        At(p + new Vector3(0, y, 0), 0.3f, 1f);
        _kit.Box(new Vector3(0.012f, 0.03f, 0.05f), new Color("e2b64a"));
    }

    /// <summary>A brick clock tower with a green copper spire.</summary>
    private void AddClockTower(Vector3 p)
    {
        var brick = _style.Architecture == Architecture.Nordic ? new Color("8a4b3a") : new Color("a8634a");
        var copper = new Color("6fae9a");
        At(p + new Vector3(0, -0.01f, 0), 0.2f, 1f);
        _kit.Box(new Vector3(0.14f, 0.62f, 0.14f), brick, brick.Lightened(0.05f));
        for (var level = 1; level <= 3; level++)
        {
            At(p + new Vector3(0, level * 0.16f, 0), 0.2f, 1f);
            _kit.Box(new Vector3(0.15f, 0.012f, 0.15f), brick.Lightened(0.2f));
        }

        // Clock faces on all four sides.
        for (var k = 0; k < 4; k++)
        {
            var yaw = 0.2f + (k * Mathf.Pi / 2f);
            At(p + (new Basis(Vector3.Up, yaw) * new Vector3(0, 0.5f, 0.072f)), new Basis(Vector3.Up, yaw) * new Basis(Vector3.Right, Mathf.Pi / 2f));
            _kit.Frustum(12, 0.04f, 0.04f, 0.006f, new Color("f6efe0"));
        }

        _kit.Sway = Sway.Roof;
        At(p + new Vector3(0, 0.61f, 0), 0.2f, 1f);
        _kit.Frustum(4, 0.11f, 0f, 0.3f, copper, twist: Mathf.Pi / 4f);
        At(p + new Vector3(0, 0.9f, 0), 0.2f, 1f);
        _kit.Box(new Vector3(0.006f, 0.06f, 0.006f), new Color("e2b64a"));
    }

    /// <summary>A white stepped tower with planted terraces and a blue-tiled dome.</summary>
    private void AddDomedTower(Vector3 p)
    {
        var white = new Color("f6f0e2");
        var tile = new Color("3d9ad1");
        var y = -0.01f;
        float[] widths = [0.2f, 0.16f, 0.12f, 0.09f];
        for (var i = 0; i < widths.Length; i++)
        {
            At(p + new Vector3(0, y, 0), 0.1f, 1f);
            _kit.Box(new Vector3(widths[i], 0.13f, widths[i]), white);
            y += 0.13f;
            _kit.Sway = Sway.Bush;
            At(p + new Vector3(0, y, 0), 0.1f, 1f);
            _kit.Blob(new Vector3(widths[i] * 0.45f, 0.015f, widths[i] * 0.45f), Pick(_style.Leaves), i, 0.2f);
            _kit.Sway = Sway.Building;
        }

        _kit.Sway = Sway.Roof;
        At(p + new Vector3(0, y, 0), 0.1f, 1f);
        _kit.Blob(new Vector3(0.06f, 0.07f, 0.06f), tile, 7, 0f);
        At(p + new Vector3(0, y + 0.06f, 0), 0.1f, 1f);
        _kit.Frustum(6, 0.008f, 0f, 0.08f, new Color("e2b64a"));
    }

    /// <summary>A mud-brick tower with timber beams poking out of its walls and crenellated parapets.</summary>
    private void AddAdobeTower(Vector3 p)
    {
        var mud = new Color("c88752");
        var beam = new Color("6b4a32");
        At(p + new Vector3(0, -0.01f, 0), 0.4f, 1f);
        _kit.Frustum(4, 0.15f, 0.1f, 0.6f, mud, mud.Lightened(0.08f), twist: Mathf.Pi / 4f);
        for (var row = 0; row < 4; row++)
        {
            for (var k = 0; k < 4; k++)
            {
                var yaw = 0.4f + (k * Mathf.Pi / 2f);
                var radius = 0.13f - (row * 0.012f);
                At(p + (new Basis(Vector3.Up, yaw) * new Vector3(0, 0.1f + (row * 0.13f), radius)), yaw, 1f);
                _kit.Box(new Vector3(0.012f, 0.012f, 0.06f), beam);
            }
        }

        for (var k = 0; k < 4; k++)
        {
            var yaw = 0.4f + (k * Mathf.Pi / 2f) + (Mathf.Pi / 4f);
            At(p + (new Basis(Vector3.Up, yaw) * new Vector3(0, 0.6f, 0.07f)), yaw, 1f);
            _kit.Frustum(4, 0.02f, 0.006f, 0.09f, mud.Lightened(0.05f));
        }
    }

    /// <summary>Glass towers come square and banded, round, or stepped in setbacks.</summary>
    private void AddTowerShape(Vector3 p, float scale)
    {
        switch (_rng.RandiRange(0, 2))
        {
            case 0:
                AddGlassTower(p, scale);
                break;
            case 1:
                AddRoundTower(p, scale);
                break;
            default:
                AddSteppedTower(p, scale);
                break;
        }
    }

    private Color TowerGlass() => _style.Architecture switch
    {
        Architecture.Japanese => Pick(new[] { new Color("9aa7b2"), new Color("7d8f9e"), new Color("b4c0c8") }),
        Architecture.Tropical => Pick(new[] { new Color("8fd0d6"), new Color("a6dbe0"), new Color("7fb8c9") }),
        Architecture.African => Pick(new[] { new Color("b9805a"), new Color("8fa9b8"), new Color("d0a074") }),
        _ => Pick(new[] { new Color("7fa7c4"), new Color("8bb8c9"), new Color("6f93ad"), new Color("a6c3cf") })
    };

    private void AddRoundTower(Vector3 p, float scale)
    {
        _kit.Sway = Sway.Building;
        var glass = TowerGlass();
        var height = _rng.RandfRange(0.4f, 0.6f);
        At(p + new Vector3(0, -0.01f, 0), 0f, scale);
        _kit.Frustum(12, 0.085f, 0.085f, 0.05f, CityWall());
        var bands = Mathf.RoundToInt(height / 0.05f);
        for (var i = 0; i < bands; i++)
        {
            At(p + new Vector3(0, (0.04f + (i * height / bands)) * scale, 0), 0f, scale);
            _kit.Frustum(12, 0.06f, 0.06f, height / bands, i % 2 == 0 ? glass : glass.Lightened(0.15f));
        }

        _kit.Sway = Sway.Roof;
        At(p + new Vector3(0, (0.04f + height) * scale, 0), 0f, scale);
        _kit.Frustum(12, 0.062f, 0.02f, 0.06f, glass.Darkened(0.2f));
    }

    private void AddSteppedTower(Vector3 p, float scale)
    {
        _kit.Sway = Sway.Building;
        var glass = TowerGlass();
        var stone = CityWall();
        var yaw = HouseYaw(p);
        var y = -0.01f;
        float[] widths = [0.13f, 0.1f, 0.075f];
        float[] heights = [0.2f, 0.18f, 0.14f];
        for (var i = 0; i < widths.Length; i++)
        {
            At(p + new Vector3(0, y * scale, 0), yaw, scale);
            _kit.Box(new Vector3(widths[i], heights[i], widths[i]), i % 2 == 0 ? stone : glass, stone.Lightened(0.1f));
            y += heights[i];
        }

        At(p + new Vector3(0, y * scale, 0), yaw, scale);
        _kit.Frustum(4, 0.03f, 0f, 0.1f, stone.Darkened(0.15f), twist: Mathf.Pi / 4f);
    }

    /// <summary>The roof an apartment block wears in this island's style.</summary>
    private void ApartmentRoof(Vector3 p, float yaw, float scale, float w, float d, float h, Color wall)
    {
        switch (_style.Architecture)
        {
            case Architecture.Nordic:
                _kit.Sway = Sway.Roof;
                At(p + new Vector3(0, h, 0), yaw, scale);
                _kit.GableRoof(w * 1.08f, d * 1.15f, 0.07f, Roof(), wall.Darkened(0.08f));
                return;
            case Architecture.Japanese:
                _kit.Sway = Sway.Roof;
                At(p + new Vector3(0, h, 0), yaw, scale);
                _kit.HipRoof(w * 1.18f, d * 1.22f, 0.045f, new Color("3d4650"), new Color("2a3038"));
                return;
            case Architecture.European when _rng.Randf() < 0.45f:
                // Mansard: steep slate roof with a flat top.
                _kit.Sway = Sway.Roof;
                At(p + new Vector3(0, h, 0), yaw, scale);
                _kit.HipRoof(w * 1.04f, d * 1.06f, 0.05f, new Color("5d6874"), new Color("454e58"));
                return;
        }

        At(p + new Vector3(0, h, 0), yaw, scale);
        _kit.Box(new Vector3(w + 0.012f, 0.012f, d + 0.012f), wall.Darkened(0.12f));
        if (_style.Architecture == Architecture.African)
        {
            for (var k = 0; k < 4; k++)
            {
                At(Local(p, yaw, new Vector3(((k % 2) - 0.5f) * w * 0.9f, h + 0.012f, ((k / 2) - 0.5f) * d * 0.9f), scale), yaw, scale);
                _kit.Box(new Vector3(0.016f, 0.02f, 0.016f), wall);
            }

            return;
        }

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

    /// <summary>A city park: lawn, ring path, a fountain and shade trees.</summary>
    private void AddCityPark(Vector3 p, float scale)
    {
        _kit.Sway = Sway.Rigid;
        At(p + new Vector3(0, -0.004f, 0), 0f, scale);
        _kit.Frustum(12, 0.13f, 0.13f, 0.01f, new Color("8fc26a"), new Color("9bd174"));
        At(p + new Vector3(0, 0.002f, 0), 0f, scale);
        _kit.Frustum(12, 0.09f, 0.09f, 0.01f, new Color("dccba3"));
        At(p + new Vector3(0, 0.004f, 0), 0f, scale);
        _kit.Frustum(12, 0.075f, 0.075f, 0.01f, new Color("8fc26a"), new Color("9bd174"));
        At(p, 0f, scale);
        _kit.Frustum(10, 0.03f, 0.03f, 0.025f, Palette.Stone, new Color("6fc6d9"));
        for (var k = 0; k < 5; k++)
        {
            var a = (k * Mathf.Tau / 5f) + 0.3f;
            var t = p + (new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * 0.11f * scale);
            t.Y = _sampler.Evaluate(t.X, t.Z).Height;
            AddTree(PickWeighted(_style.VillageTrees), t, scale * 0.75f);
        }
    }

    /// <summary>A stadium for a Metropolis: oval stands around a green pitch, with floodlights.</summary>
    private void AddStadium(Vector3 p, float scale)
    {
        _kit.Sway = Sway.Building;
        var yaw = HouseYaw(p);
        var stand = new Color("e6e1d6");
        var seats = Pick(new[] { new Color("c4452f"), new Color("3d6fb5"), new Color("e2b64a") });
        var basis = new Basis(Vector3.Up, yaw).Scaled(new Vector3(1.4f, 1f, 1f) * scale);
        _kit.Transform = new Transform3D(basis, p + new Vector3(0, -0.01f, 0));
        _kit.Frustum(16, 0.13f, 0.15f, 0.07f, stand, seats);
        _kit.Transform = new Transform3D(basis, p + new Vector3(0, 0.05f, 0));
        _kit.Frustum(16, 0.1f, 0.1f, 0.025f, new Color("5aa847"), new Color("6cbf55"));
        for (var k = 0; k < 4; k++)
        {
            var a = (k * Mathf.Pi / 2f) + (Mathf.Pi / 4f);
            var post = p + (new Basis(Vector3.Up, yaw) * new Vector3(Mathf.Cos(a) * 0.2f, 0, Mathf.Sin(a) * 0.15f) * scale);
            At(post, yaw, scale);
            _kit.Box(new Vector3(0.008f, 0.16f, 0.008f), new Color("9aa4ad"));
            At(post + new Vector3(0, 0.16f * scale, 0), yaw, scale);
            _kit.Box(new Vector3(0.03f, 0.015f, 0.01f), new Color("fff2b0"));
        }
    }
}
