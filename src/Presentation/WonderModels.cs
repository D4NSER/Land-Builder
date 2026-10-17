using System;
using System.Collections.Generic;
using Godot;
using LandBuilder.Domain;

namespace LandBuilder.Presentation;

/// <summary>
/// Builds the big landmark models for wonders, the scaffolding shown on blueprint plots while they are filled, and
/// the surroundings that tie a finished wonder's plots together. Landmarks are authored at tile size and scaled up
/// over the footprint by <see cref="WonderLayout"/>. Pop-in delays are staged bottom-up, so completing a wonder plays
/// as a little construction sequence.
/// </summary>
public sealed partial class WonderModelBuilder
{
    private static readonly Color Stone = new("c9c2b4");
    private static readonly Color StoneDark = new("9c958a");
    private static readonly Color Slate = new("4a5a6e");
    private static readonly Color Wood = new("8b6a4a");
    private static readonly Color Gold = new("e2b64a");

    private readonly MeshKit _kit = new();
    private readonly Func<float, float, float> _ground;
    private readonly ThemeStyle _style;
    private readonly Node3D _partsRoot;
    private readonly List<AnimatedPart> _parts = new();
    private readonly RandomNumberGenerator _rng = new();
    private readonly float _water;

    /// <param name="waterLevel">The lake surface in this builder's (possibly scaled) frame.</param>
    public WonderModelBuilder(Func<float, float, float> ground, ThemeStyle style, Node3D partsRoot, int seed, float waterLevel = TerrainSampler.WaterLevel)
    {
        _water = waterLevel;
        _ground = ground;
        _style = style;
        _partsRoot = partsRoot;
        _rng.Seed = (ulong)(uint)seed + 99UL;
    }

    public IReadOnlyList<AnimatedPart> Parts => _parts;

    /// <summary>Scaffolding, a half-built core and a crane: one plot of a blueprint that has its tile.</summary>
    public ArrayMesh BuildScaffold(WonderType type)
    {
        BuildSite(type);
        return _kit.Commit(withCustom: true);
    }

    /// <summary>The finished landmark at the centre of the footprint.</summary>
    public ArrayMesh Build(WonderType type)
    {
        switch (type)
        {
            case WonderType.Castle:
                BuildCastle();
                break;
            case WonderType.Cathedral:
                BuildCathedral();
                break;
            case WonderType.GreatPagoda:
                BuildGreatPagoda();
                break;
            case WonderType.Harbour:
                BuildHarbour();
                break;
            case WonderType.Lighthouse:
                BuildLighthouse();
                break;
            case WonderType.RocketPad:
                BuildRocketPad();
                break;
            case WonderType.Observatory:
                BuildObservatory();
                break;
            case WonderType.BotanicalPark:
                BuildBotanicalPark();
                break;
            case WonderType.TallShip:
                BuildGalleon();
                break;
        }

        return _kit.Commit(withCustom: true);
    }

    // ---------------------------------------------------------------- helpers

    private static Vector3 Dir(float degrees) => HexLayout.AngleToDirection(Mathf.DegToRad(degrees));

    private Vector3 OnGround(float x, float z) => new(x, _ground(x, z), z);

    private Vector3 OnGround(Vector3 p) => OnGround(p.X, p.Z);

    /// <summary>Sets the pivot and pop-in delay (0 = first, 1 = last) for the geometry that follows.</summary>
    private void Stage(Vector3 pivot, float delay, float sway = Sway.Building)
    {
        _kit.Custom = new Color(pivot.X, pivot.Y, pivot.Z, Mathf.Clamp(delay, 0f, 1f));
        _kit.Sway = sway;
    }

    private void At(Vector3 p, float yaw = 0f, float scale = 1f) =>
        _kit.Transform = new Transform3D(new Basis(Vector3.Up, yaw).Scaled(Vector3.One * scale), p);

    private void At(Vector3 p, Basis basis) => _kit.Transform = new Transform3D(basis, p);

    private void Plaza(float radius, Color color, float delay = 0f)
    {
        var c = OnGround(0, 0);
        Stage(c, delay, Sway.Rigid);
        At(c - new Vector3(0, 0.02f, 0));
        _kit.Frustum(6, radius, radius * 0.97f, 0.035f, color.Darkened(0.1f), color, twist: Mathf.Pi / 6f);
    }

    /// <summary>Straight wall between two points with crenellations along the top.</summary>
    private void Wall(Vector3 a, Vector3 b, float height, float thickness, Color color, float delay)
    {
        var d = b - a;
        var len = new Vector2(d.X, d.Z).Length();
        var yaw = Mathf.Atan2(-d.Z, d.X);
        var mid = OnGround((a + b) / 2f);
        Stage(mid, delay);
        At(mid - new Vector3(0, 0.02f, 0), yaw);
        _kit.Box(new Vector3(len, height + 0.02f, thickness), color, color.Lightened(0.05f));
        var crenels = Mathf.Max(2, Mathf.RoundToInt(len / 0.045f));
        for (var i = 0; i < crenels; i += 2)
        {
            var t = (i + 0.5f) / crenels;
            At(a.Lerp(b, t) with { Y = mid.Y + height }, yaw);
            _kit.Box(new Vector3(len / crenels, 0.02f, thickness), color);
        }
    }

    private void RoundTower(Vector3 at, float radius, float height, Color wall, Color? roof, float delay)
    {
        var p = OnGround(at);
        Stage(p, delay);
        At(p - new Vector3(0, 0.02f, 0));
        _kit.Frustum(10, radius, radius * 0.92f, height + 0.02f, wall, wall.Lightened(0.05f));
        if (roof is Color r)
        {
            At(p + new Vector3(0, height, 0));
            _kit.Sway = Sway.Roof;
            _kit.Frustum(10, radius * 1.25f, 0f, height * 0.5f, r, capBottom: true, bottom: r.Darkened(0.3f));
        }
        else
        {
            for (var i = 0; i < 8; i += 2)
            {
                var a = i * Mathf.Tau / 8f;
                At(p + new Vector3(Mathf.Cos(a) * radius * 0.85f, height, Mathf.Sin(a) * radius * 0.85f), a);
                _kit.Box(new Vector3(0.025f, 0.025f, 0.025f), wall);
            }
        }
    }

    private void Flag(Vector3 top, Color color, float delay)
    {
        Stage(top, delay, Sway.Grass);
        At(top);
        _kit.Box(new Vector3(0.005f, 0.09f, 0.005f), new Color("4a4a4a"));
        At(top + new Vector3(0.025f, 0.06f, 0));
        _kit.Box(new Vector3(0.05f, 0.03f, 0.003f), color);
    }

    private void Tree(Vector3 at, float scale, Color leaf, float delay)
    {
        var p = OnGround(at);
        Stage(p, delay, Sway.Trunk);
        At(p, 0, scale);
        _kit.Frustum(5, 0.02f, 0.015f, 0.1f, Palette.TrunkBrown);
        _kit.Sway = Sway.Deciduous;
        At(p + new Vector3(0, 0.13f * scale, 0), 0, scale);
        _kit.Blob(new Vector3(0.08f, 0.075f, 0.08f), leaf, _rng.RandiRange(0, 999));
    }

    private void Palm(Vector3 at, float scale, float delay)
    {
        var p = OnGround(at);
        Stage(p, delay, Sway.Trunk);
        At(p, 0, scale);
        _kit.Frustum(5, 0.014f, 0.01f, 0.22f, new Color("9c7650"));
        _kit.Sway = Sway.Evergreen;
        _kit.OrientFrom = null;
        for (var i = 0; i < 6; i++)
        {
            var b = new Basis(Vector3.Up, i * Mathf.Tau / 6f) * new Basis(Vector3.Right, -0.35f);
            _kit.Transform = new Transform3D(b.Scaled(Vector3.One * scale), p + new Vector3(0, 0.22f * scale, 0));
            _kit.Triangle(new Vector3(-0.02f, 0, 0), new Vector3(0.02f, 0, 0), new Vector3(0, -0.04f, 0.12f), new Color("5aa847"));
            _kit.Triangle(new Vector3(0.02f, 0, 0), new Vector3(-0.02f, 0, 0), new Vector3(0, -0.04f, 0.12f), new Color("4a8f3a"));
        }
    }

    private Node3D AddPart(AnimatedPartKind kind, string name, MeshKit kit, Transform3D transform, Material? material = null)
    {
        var node = new Node3D { Name = name, Transform = transform };
        var mesh = new MeshInstance3D { Mesh = kit.Commit(withCustom: true) };
        if (material is not null)
            mesh.SetMeta("custom_material", true);
        mesh.MaterialOverride = material;
        node.AddChild(mesh);
        _partsRoot.AddChild(node);
        _parts.Add(new AnimatedPart(kind, node, _rng.Randf() * Mathf.Tau, node.Position));
        return node;
    }

    // ---------------------------------------------------------------- construction site

    private void BuildSite(WonderType type)
    {
        var tall = type is WonderType.Lighthouse or WonderType.RocketPad or WonderType.GreatPagoda ? 1.4f : 1f;
        Plaza(0.58f, new Color("b39a72"));

        // Fence posts around the lot.
        for (var i = 0; i < 18; i++)
        {
            var a = i * Mathf.Tau / 18f;
            var p = OnGround(Mathf.Cos(a) * 0.6f, Mathf.Sin(a) * 0.6f);
            Stage(p, 0.05f, Sway.Rigid);
            At(p, a);
            _kit.Box(new Vector3(0.008f, 0.04f, 0.008f), Wood);
        }

        // Foundations.
        var c = OnGround(0, 0);
        Stage(c, 0.1f);
        At(c);
        _kit.Box(new Vector3(0.36f, 0.05f, 0.3f), StoneDark);

        // Scaffolding: poles, rails and planks around a half-built core.
        Stage(c, 0.2f);
        At(c + new Vector3(0, 0.05f, 0));
        _kit.Box(new Vector3(0.24f, 0.14f * tall, 0.2f), Stone);
        float[] xs = [-0.2f, 0f, 0.2f];
        float[] zs = [-0.17f, 0.17f];
        foreach (var x in xs)
        {
            foreach (var z in zs)
            {
                var p = c + new Vector3(x, 0, z);
                Stage(p, 0.35f, Sway.Rigid);
                At(p);
                _kit.Box(new Vector3(0.008f, 0.32f * tall, 0.008f), Wood);
            }
        }

        for (var level = 1; level <= 3; level++)
        {
            var y = level * 0.1f * tall;
            foreach (var z in zs)
            {
                Stage(c, 0.4f + (level * 0.1f), Sway.Rigid);
                At(c + new Vector3(0, y, z));
                _kit.Box(new Vector3(0.42f, 0.008f, 0.03f), new Color("c8a070"));
            }
        }

        // Material stacks.
        for (var i = 0; i < 3; i++)
        {
            var p = OnGround(Dir(200 + (i * 40)) * 0.42f);
            Stage(p, 0.3f, Sway.Rigid);
            At(p, i);
            _kit.Box(new Vector3(0.06f, 0.03f + (i * 0.01f), 0.05f), i == 1 ? Wood : Stone);
        }

        // Crane: a mast in the static mesh and a slewing jib as an animated part.
        var mastBase = OnGround(Dir(320) * 0.38f);
        var mastHeight = 0.6f * tall;
        Stage(mastBase, 0.2f, Sway.Rigid);
        At(mastBase);
        _kit.Box(new Vector3(0.03f, mastHeight, 0.03f), new Color("e2b64a"));
        var jib = new MeshKit();
        jib.Transform = new Transform3D(Basis.Identity, new Vector3(0.12f, 0, 0));
        jib.Box(new Vector3(0.5f, 0.025f, 0.025f), new Color("e2b64a"));
        jib.Transform = new Transform3D(Basis.Identity, new Vector3(-0.1f, -0.02f, 0));
        jib.Box(new Vector3(0.06f, 0.04f, 0.04f), new Color("6b6b6b"));
        jib.Transform = new Transform3D(Basis.Identity, new Vector3(0.3f, -0.18f, 0));
        jib.Box(new Vector3(0.004f, 0.18f, 0.004f), new Color("3a3a3a"));
        jib.Transform = new Transform3D(Basis.Identity, new Vector3(0.3f, -0.2f, 0));
        jib.Box(new Vector3(0.04f, 0.025f, 0.04f), StoneDark);
        AddPart(AnimatedPartKind.Crane, "Crane", jib, new Transform3D(Basis.Identity, mastBase + new Vector3(0, mastHeight, 0)));
    }

    // ---------------------------------------------------------------- castle

    private void BuildCastle()
    {
        Plaza(0.62f, new Color("a9a296"));
        var stone = Stone;
        var roof = _style.Theme == WorldTheme.Nordic ? new Color("3a2f28") : Slate;
        // Curtain wall with six corner towers, centred on the village half of the tile.
        var centre = Dir(90) * 0.04f;
        var corners = new Vector3[6];
        for (var i = 0; i < 6; i++)
            corners[i] = centre + (Dir(30 + (i * 60)) * 0.46f);
        for (var i = 0; i < 6; i++)
            Wall(corners[i], corners[(i + 1) % 6], 0.13f, 0.035f, stone, 0.15f + (i * 0.03f));
        for (var i = 0; i < 6; i++)
            RoundTower(corners[i], 0.055f, 0.2f, stone, roof, 0.3f + (i * 0.04f));

        // Gatehouse.
        var gate = OnGround(Dir(270) * 0.42f + centre);
        Stage(gate, 0.45f);
        At(gate - new Vector3(0, 0.02f, 0), Mathf.DegToRad(270));
        _kit.Box(new Vector3(0.12f, 0.19f, 0.08f), stone);
        At(gate + new Vector3(0, 0.02f, 0) + (Dir(270) * 0.041f), Mathf.DegToRad(270));
        _kit.Box(new Vector3(0.05f, 0.08f, 0.004f), new Color("3a2a20"));

        // Keep with its own corner turrets and a banner.
        var keep = OnGround(centre);
        Stage(keep, 0.55f);
        At(keep - new Vector3(0, 0.02f, 0), 0.4f);
        _kit.Box(new Vector3(0.2f, 0.42f, 0.2f), stone.Lightened(0.05f));
        for (var i = 0; i < 4; i++)
        {
            var a = 0.4f + (Mathf.Pi / 4f) + (i * Mathf.Pi / 2f);
            RoundTower(keep + (new Vector3(Mathf.Cos(a), 0, -Mathf.Sin(a)) * 0.14f), 0.035f, 0.47f, stone, roof, 0.7f + (i * 0.04f));
        }

        Stage(keep, 0.85f, Sway.Roof);
        At(keep + new Vector3(0, 0.4f, 0), 0.4f);
        _kit.HipRoof(0.22f, 0.22f, 0.12f, roof);
        Flag(keep + new Vector3(0, 0.52f, 0), new Color("c4452f"), 0.95f);
    }

    // ---------------------------------------------------------------- cathedral

    private void BuildCathedral()
    {
        Plaza(0.6f, new Color("cfc6b2"));
        var stone = new Color("e3dccb");
        var roof = Slate;
        var c = OnGround(0, 0);
        const float yaw = 0f;

        // Nave and aisles.
        Stage(c, 0.1f);
        At(c - new Vector3(0, 0.02f, 0), yaw);
        _kit.Box(new Vector3(0.52f, 0.24f, 0.17f), stone);
        Stage(c, 0.15f);
        At(c - new Vector3(0, 0.02f, 0), yaw);
        _kit.Box(new Vector3(0.48f, 0.13f, 0.27f), stone.Darkened(0.05f));
        Stage(c, 0.5f, Sway.Roof);
        At(c + new Vector3(0, 0.22f, 0), yaw);
        _kit.GableRoof(0.54f, 0.2f, 0.12f, roof, stone);
        At(c + new Vector3(0, 0.11f, 0), yaw);
        _kit.GableRoof(0.5f, 0.3f, 0.05f, roof.Lightened(0.05f), stone);

        // Transept.
        Stage(c, 0.2f);
        At(c + new Vector3(0.05f, -0.02f, 0), Mathf.Pi / 2f);
        _kit.Box(new Vector3(0.4f, 0.22f, 0.13f), stone);
        Stage(c, 0.55f, Sway.Roof);
        At(c + new Vector3(0.05f, 0.2f, 0), Mathf.Pi / 2f);
        _kit.GableRoof(0.42f, 0.16f, 0.1f, roof, stone);

        // Crossing spire and apse.
        Stage(c, 0.7f);
        At(c + new Vector3(0.05f, 0.24f, 0));
        _kit.Frustum(8, 0.05f, 0.045f, 0.08f, stone);
        Stage(c, 0.8f, Sway.Roof);
        At(c + new Vector3(0.05f, 0.32f, 0));
        _kit.Frustum(8, 0.055f, 0f, 0.2f, roof);
        Stage(c, 0.3f);
        At(c + new Vector3(0.26f, -0.02f, 0));
        _kit.Frustum(10, 0.085f, 0.085f, 0.2f, stone);
        At(c + new Vector3(0.26f, 0.18f, 0));
        _kit.Sway = Sway.Roof;
        _kit.Frustum(10, 0.095f, 0f, 0.09f, roof);

        // Twin west towers with spires and a rose window.
        foreach (var side in new[] { -1f, 1f })
        {
            var t = c + new Vector3(-0.3f, 0, side * 0.08f);
            Stage(t, 0.35f);
            At(t - new Vector3(0, 0.02f, 0));
            _kit.Box(new Vector3(0.11f, 0.42f, 0.11f), stone);
            Stage(t, 0.9f, Sway.Roof);
            At(t + new Vector3(0, 0.4f, 0), Mathf.Pi / 4f);
            _kit.Frustum(4, 0.08f, 0f, 0.26f, roof);
        }

        Stage(c, 0.6f);
        At(c + new Vector3(-0.356f, 0.25f, 0), new Basis(Vector3.Back, Mathf.Pi / 2f));
        _kit.Frustum(12, 0.045f, 0.045f, 0.004f, new Color("6a8fc9"), Gold);
    }

    // ---------------------------------------------------------------- great pagoda

    private void BuildGreatPagoda()
    {
        Plaza(0.58f, new Color("b9b3a6"));
        var c = OnGround(Dir(0) * -0.05f);
        var roof = new Color("3d4757");
        var red = new Color("c4452f");

        // Low enclosure wall with a torii at the entrance.
        for (var i = 0; i < 6; i++)
        {
            var a = OnGround(Dir(30 + (i * 60)) * 0.5f);
            var b = OnGround(Dir(90 + (i * 60)) * 0.5f);
            if (i == 4)
                continue;
            var mid = (a + b) / 2f;
            var d = b - a;
            Stage(mid, 0.1f);
            At(mid, Mathf.Atan2(-d.Z, d.X));
            _kit.Box(new Vector3(new Vector2(d.X, d.Z).Length(), 0.05f, 0.02f), new Color("f2eee6"));
            At(mid + new Vector3(0, 0.05f, 0), Mathf.Atan2(-d.Z, d.X));
            _kit.Sway = Sway.Roof;
            _kit.GableRoof(new Vector2(d.X, d.Z).Length() * 1.05f, 0.05f, 0.02f, roof, roof);
        }

        var gate = OnGround(Dir(270) * 0.5f);
        Stage(gate, 0.2f);
        foreach (var s in new[] { -1f, 1f })
        {
            At(gate + new Vector3(s * 0.07f, 0, 0));
            _kit.Frustum(8, 0.009f, 0.008f, 0.17f, red);
        }

        At(gate + new Vector3(0, 0.14f, 0));
        _kit.Box(new Vector3(0.17f, 0.012f, 0.014f), red);
        At(gate + new Vector3(0, 0.165f, 0));
        _kit.Box(new Vector3(0.21f, 0.014f, 0.02f), new Color("2b2b2b"));

        // Seven tiers.
        var y = 0f;
        Stage(c, 0.2f);
        At(c - new Vector3(0, 0.02f, 0));
        _kit.Box(new Vector3(0.24f, 0.05f, 0.24f), new Color("8f8a80"));
        y = 0.03f;
        for (var i = 0; i < 7; i++)
        {
            var s = 0.17f - (i * 0.016f);
            Stage(c, 0.3f + (i * 0.08f));
            At(c + new Vector3(0, y, 0), 0.3f);
            _kit.Box(new Vector3(s, 0.065f, s), red);
            y += 0.065f;
            Stage(c, 0.34f + (i * 0.08f), Sway.Roof);
            At(c + new Vector3(0, y, 0), 0.3f);
            _kit.HipRoof(s * 1.85f, s * 1.85f, 0.04f, roof);
            y += 0.015f;
        }

        Stage(c, 0.95f, Sway.Rigid);
        At(c + new Vector3(0, y, 0));
        _kit.Frustum(6, 0.01f, 0.005f, 0.18f, Gold);

        Tree(Dir(200) * 0.36f, 1f, new Color("f9c3d5"), 0.6f);
        Tree(Dir(340) * 0.36f, 1f, new Color("fbd9e5"), 0.65f);
    }

    // ---------------------------------------------------------------- harbour

    private void BuildHarbour()
    {
        // Quay along the village half (sectors 3-5); docks reach into the lake half (sectors 0-2).
        var quayStone = new Color("a9a296");
        for (var i = 0; i < 3; i++)
        {
            var p = OnGround(Dir(180 + (i * 60)) * 0.35f);
            Stage(p, 0.1f + (i * 0.05f));
            At(p - new Vector3(0, 0.02f, 0), Mathf.DegToRad(180 + (i * 60)));
            _kit.Box(new Vector3(0.26f, 0.13f, 0.18f), Palette.WallWarm);
            Stage(p, 0.3f + (i * 0.05f), Sway.Roof);
            At(p + new Vector3(0, 0.11f, 0), Mathf.DegToRad(180 + (i * 60)));
            _kit.GableRoof(0.28f, 0.2f, 0.07f, new Color("a8452f"), Palette.WallWarm);
        }

        // Wooden piers on piles.
        var deck = _water + 0.03f;
        foreach (var angle in new[] { 20f, 60f, 100f })
        {
            var d = Dir(angle);
            var start = d * 0.1f;
            var end = d * 0.72f;
            var mid = (start + end) / 2f;
            var yaw = Mathf.Atan2(-d.Z, d.X);
            Stage(new Vector3(mid.X, deck, mid.Z), 0.25f, Sway.Rigid);
            At(new Vector3(mid.X, deck, mid.Z), yaw);
            _kit.Box(new Vector3(0.62f, 0.012f, 0.06f), Wood);
            for (var k = 0; k < 6; k++)
            {
                var pile = start.Lerp(end, k / 5f);
                At(new Vector3(pile.X, deck - 0.12f, pile.Z));
                _kit.Box(new Vector3(0.012f, 0.13f, 0.012f), Wood.Darkened(0.2f));
            }
        }

        // Dock crane.
        var crane = OnGround(Dir(150) * 0.15f);
        Stage(crane, 0.4f, Sway.Rigid);
        At(crane);
        _kit.Box(new Vector3(0.02f, 0.3f, 0.02f), new Color("c4452f"));
        At(crane + new Vector3(0.08f, 0.3f, 0));
        _kit.Box(new Vector3(0.2f, 0.016f, 0.016f), new Color("c4452f"));
        At(crane + new Vector3(0, 0.0f, 0));
        _kit.Box(new Vector3(0.06f, 0.04f, 0.06f), new Color("4a4a4a"));

        // Barrels and crates on the quay.
        for (var i = 0; i < 5; i++)
        {
            var p = OnGround(Dir(130 + (i * 14)) * 0.25f);
            Stage(p, 0.5f, Sway.Rigid);
            At(p, i);
            if (i % 2 == 0)
                _kit.Frustum(8, 0.018f, 0.018f, 0.04f, new Color("8b5a3b"));
            else
                _kit.Box(new Vector3(0.035f, 0.035f, 0.035f), new Color("c8a070"));
        }

        // A tall ship moored between the piers.
        var ship = new MeshKit { Custom = new Color(0, 0, 0, 0) };
        var hull = new Color("5a3a2a");
        ship.Box(new Vector3(0.36f, 0.06f, 0.11f), hull, new Color("a07a50"));
        ship.Transform = new Transform3D(Basis.Identity, new Vector3(0.18f, 0, 0));
        ship.Frustum(3, 0.055f, 0f, 0.06f, hull);
        ship.Transform = new Transform3D(Basis.Identity, new Vector3(-0.15f, 0.06f, 0));
        ship.Box(new Vector3(0.08f, 0.04f, 0.1f), hull.Lightened(0.1f));
        foreach (var (x, h) in new[] { (-0.07f, 0.34f), (0.08f, 0.3f) })
        {
            ship.Transform = new Transform3D(Basis.Identity, new Vector3(x, 0.05f, 0));
            ship.Box(new Vector3(0.012f, h, 0.012f), Wood);
            for (var s = 0; s < 2; s++)
            {
                ship.Transform = new Transform3D(Basis.Identity, new Vector3(x, 0.11f + (s * 0.11f), 0));
                ship.Box(new Vector3(0.012f, 0.09f - (s * 0.015f), 0.13f - (s * 0.03f)), new Color("f4ecdc"));
            }
        }

        var shipPos = (Dir(60) * 0.5f) with { Y = _water + 0.005f };
        AddPart(AnimatedPartKind.Boat, "Ship", ship, new Transform3D(new Basis(Vector3.Up, Mathf.DegToRad(60) + (Mathf.Pi / 2f)), shipPos));
    }

    // ---------------------------------------------------------------- lighthouse

    private void BuildLighthouse()
    {
        // On the grassy promontory (sectors 4-5), looking out over the lake.
        var at = OnGround(Dir(270) * 0.32f);
        Stage(at, 0.05f, Sway.Rigid);
        At(at - new Vector3(0, 0.02f, 0));
        _kit.Frustum(8, 0.14f, 0.13f, 0.04f, StoneDark);
        const int bands = 6;
        const float height = 0.66f;
        for (var i = 0; i < bands; i++)
        {
            var h0 = i * height / bands;
            Stage(at, 0.1f + (i * 0.08f));
            At(at + new Vector3(0, 0.02f + h0, 0));
            var r0 = Mathf.Lerp(0.085f, 0.06f, i / (float)bands);
            var r1 = Mathf.Lerp(0.085f, 0.06f, (i + 1) / (float)bands);
            _kit.Frustum(12, r0, r1, height / bands, i % 2 == 0 ? new Color("f4f1ea") : new Color("c4452f"));
        }

        var top = at + new Vector3(0, 0.02f + height, 0);
        Stage(top, 0.7f);
        At(top);
        _kit.Frustum(12, 0.08f, 0.08f, 0.012f, new Color("3a3a3a"));
        At(top + new Vector3(0, 0.012f, 0));
        _kit.Frustum(12, 0.05f, 0.05f, 0.06f, new Color("fff2b0"));
        Stage(top, 0.8f, Sway.Roof);
        At(top + new Vector3(0, 0.072f, 0));
        _kit.Frustum(12, 0.065f, 0f, 0.07f, new Color("c4452f"));

        // Keeper's cottage.
        var cottage = OnGround(Dir(210) * 0.36f);
        Stage(cottage, 0.4f);
        At(cottage - new Vector3(0, 0.02f, 0), 0.6f);
        _kit.Box(new Vector3(0.13f, 0.09f, 0.1f), Palette.Wall);
        At(cottage + new Vector3(0, 0.07f, 0), 0.6f);
        _kit.GableRoof(0.15f, 0.13f, 0.06f, new Color("c4452f"), Palette.Wall);

        // Rotating beam of light.
        var beam = new MeshKit();
        beam.Transform = new Transform3D(new Basis(Vector3.Back, Mathf.Pi / 2f), Vector3.Zero);
        beam.Frustum(8, 0.025f, 0.16f, 1.6f, Colors.White);
        var material = new StandardMaterial3D
        {
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            BlendMode = BaseMaterial3D.BlendModeEnum.Add,
            AlbedoColor = new Color(1f, 0.92f, 0.6f, 0.18f),
            CullMode = BaseMaterial3D.CullModeEnum.Disabled,
            NoDepthTest = false
        };
        AddPart(AnimatedPartKind.Beam, "Beam", beam, new Transform3D(Basis.Identity, top + new Vector3(0, 0.04f, 0)), material);
    }

    // ---------------------------------------------------------------- rocket pad

    private void BuildRocketPad()
    {
        Plaza(0.6f, new Color("9c9a95"));
        var c = OnGround(0, 0);
        // Launch tower lattice.
        var tower = c + new Vector3(-0.11f, 0, 0);
        for (var i = 0; i < 4; i++)
        {
            var corner = tower + new Vector3((i % 2 == 0 ? -1 : 1) * 0.04f, 0, (i < 2 ? -1 : 1) * 0.04f);
            Stage(corner, 0.1f + (i * 0.02f));
            At(corner);
            _kit.Box(new Vector3(0.01f, 0.72f, 0.01f), new Color("c4452f"));
        }

        for (var level = 0; level < 9; level++)
        {
            Stage(tower, 0.15f + (level * 0.06f));
            At(tower + new Vector3(0, 0.04f + (level * 0.08f), 0));
            _kit.Box(new Vector3(0.09f, 0.008f, 0.09f), new Color("c4452f"));
            var brace = new Basis(Vector3.Up, level % 2 == 0 ? 0f : Mathf.Pi / 2f) * new Basis(Vector3.Back, 0.78f);
            At(tower + new Vector3(0, 0.08f + (level * 0.08f), 0), brace);
            _kit.Box(new Vector3(0.006f, 0.11f, 0.006f), new Color("a83a28"));
        }

        Stage(tower, 0.7f);
        At(tower + new Vector3(0.05f, 0.5f, 0));
        _kit.Box(new Vector3(0.08f, 0.012f, 0.012f), new Color("c4452f"));

        // Fuel tanks and a control bunker.
        foreach (var (angle, r) in new[] { (60f, 0.36f), (90f, 0.38f) })
        {
            var p = OnGround(Dir(angle) * r);
            Stage(p, 0.3f);
            At(p - new Vector3(0, 0.02f, 0));
            _kit.Frustum(10, 0.05f, 0.05f, 0.14f, new Color("f4f1ea"));
            At(p + new Vector3(0, 0.12f, 0));
            _kit.Blob(new Vector3(0.05f, 0.03f, 0.05f), new Color("f4f1ea"), 3, 0.01f);
        }

        var bunker = OnGround(Dir(250) * 0.38f);
        Stage(bunker, 0.35f);
        At(bunker - new Vector3(0, 0.02f, 0), 0.4f);
        _kit.Box(new Vector3(0.16f, 0.07f, 0.1f), new Color("d6d2c8"));
        At(bunker + new Vector3(0, 0.05f, 0), 0.4f);
        _kit.Blob(new Vector3(0.03f, 0.02f, 0.03f), new Color("9aa4ad"), 5, 0.01f);

        // The rocket itself launches every so often (animated part).
        var rocket = new MeshKit();
        rocket.Frustum(12, 0.04f, 0.04f, 0.36f, Colors.White, Colors.White);
        rocket.Transform = new Transform3D(Basis.Identity, new Vector3(0, 0.12f, 0));
        rocket.Frustum(12, 0.041f, 0.041f, 0.02f, new Color("2b2b2b"));
        rocket.Transform = new Transform3D(Basis.Identity, new Vector3(0, 0.36f, 0));
        rocket.Frustum(12, 0.04f, 0f, 0.12f, new Color("c4452f"));
        for (var i = 0; i < 4; i++)
        {
            rocket.Transform = new Transform3D(new Basis(Vector3.Up, i * Mathf.Pi / 2f), Vector3.Zero);
            rocket.OrientFrom = null;
            var fin = new Color("c4452f");
            rocket.Triangle(new Vector3(0.04f, 0, 0), new Vector3(0.09f, 0, 0), new Vector3(0.04f, 0.1f, 0), fin);
            rocket.Triangle(new Vector3(0.04f, 0, 0), new Vector3(0.04f, 0.1f, 0), new Vector3(0.09f, 0, 0), fin);
        }

        AddPart(AnimatedPartKind.Rocket, "Rocket", rocket, new Transform3D(Basis.Identity, c + new Vector3(0.02f, 0.015f, 0)));
    }

    // ---------------------------------------------------------------- observatory

    private void BuildObservatory()
    {
        var c = OnGround(Dir(120) * 0.05f);
        Stage(c, 0.05f, Sway.Rigid);
        At(c - new Vector3(0, 0.03f, 0));
        _kit.Frustum(10, 0.2f, 0.18f, 0.06f, StoneDark);
        Stage(c, 0.2f);
        At(c + new Vector3(0, 0.03f, 0));
        _kit.Frustum(16, 0.13f, 0.13f, 0.12f, new Color("f4f1ea"));
        Stage(c, 0.5f, Sway.Roof);
        At(c + new Vector3(0, 0.15f, 0));
        _kit.Blob(new Vector3(0.135f, 0.12f, 0.135f), new Color("e8eef2"), 1, 0.0f);
        At(c + new Vector3(0, 0.2f, 0.0f), new Basis(Vector3.Right, -0.6f));
        _kit.Box(new Vector3(0.03f, 0.09f, 0.004f), new Color("2b3440"));

        // Telescope poking out of the slit.
        Stage(c, 0.7f, Sway.Rigid);
        At(c + new Vector3(0, 0.22f, 0.03f), new Basis(Vector3.Right, -0.7f));
        _kit.Frustum(8, 0.018f, 0.015f, 0.12f, new Color("6b6b6b"));

        // Radio dish on a mast.
        var dish = OnGround(Dir(300) * 0.34f);
        Stage(dish, 0.4f);
        At(dish);
        _kit.Box(new Vector3(0.015f, 0.14f, 0.015f), new Color("c9c4bb"));
        At(dish + new Vector3(0, 0.15f, 0), new Basis(Vector3.Forward, 0.6f));
        _kit.Frustum(12, 0.01f, 0.08f, 0.03f, new Color("f4f1ea"));

        // Path of lanterns up the slope.
        for (var i = 0; i < 4; i++)
        {
            var p = OnGround(Dir(240) * (0.24f + (i * 0.1f)));
            Stage(p, 0.3f + (i * 0.05f), Sway.Rigid);
            At(p);
            _kit.Box(new Vector3(0.008f, 0.04f, 0.008f), new Color("3a3a3a"));
            At(p + new Vector3(0, 0.04f, 0));
            _kit.Box(new Vector3(0.016f, 0.016f, 0.016f), new Color("ffe7a3"));
        }
    }

    // ---------------------------------------------------------------- botanical park

    private void BuildBotanicalPark()
    {
        Plaza(0.6f, new Color("9fc96c"));
        // Gravel paths in a ring and a cross.
        var c = OnGround(0, 0);
        for (var i = 0; i < 24; i++)
        {
            var a0 = i * Mathf.Tau / 24f;
            var p = OnGround(Mathf.Cos(a0) * 0.42f, -Mathf.Sin(a0) * 0.42f);
            Stage(p, 0.05f, Sway.Rigid);
            At(p + new Vector3(0, 0.012f, 0), a0 + (Mathf.Pi / 2f));
            _kit.Box(new Vector3(0.12f, 0.004f, 0.05f), new Color("e3d6b4"));
        }

        // Fountain in the lake sectors.
        var fountain = OnGround(Dir(270) * 0.3f);
        Stage(fountain, 0.2f, Sway.Rigid);
        At(fountain);
        _kit.Frustum(10, 0.08f, 0.08f, 0.03f, Stone, new Color("6fc6d9"));
        At(fountain + new Vector3(0, 0.03f, 0));
        _kit.Frustum(8, 0.015f, 0.01f, 0.06f, Stone);
        At(fountain + new Vector3(0, 0.09f, 0));
        _kit.Blob(new Vector3(0.025f, 0.02f, 0.025f), new Color("d9f2f7"), 2, 0.1f);

        // Flower beds.
        Color[] beds = [new("e86f8a"), new("f2c94c"), new("9b7ede"), new("ff9671")];
        for (var i = 0; i < 4; i++)
        {
            var p = OnGround(Dir(20 + (i * 45)) * 0.5f);
            Stage(p, 0.3f + (i * 0.05f), Sway.Flower);
            At(p, i);
            _kit.Blob(new Vector3(0.07f, 0.02f, 0.05f), beds[i], i, 0.2f);
        }

        // Palm house: a stone base with palms, under a glass dome (separate transparent part).
        var house = OnGround(Dir(60) * 0.1f);
        Stage(house, 0.2f);
        At(house - new Vector3(0, 0.02f, 0));
        _kit.Frustum(16, 0.2f, 0.2f, 0.05f, Palette.Wall);
        Palm(house + new Vector3(0.05f, 0.03f, 0.02f), 0.9f, 0.5f);
        Palm(house + new Vector3(-0.06f, 0.03f, -0.03f), 0.8f, 0.55f);
        Stage(house, 0.6f, Sway.Rigid);
        for (var i = 0; i < 8; i++)
        {
            var a = i * Mathf.Tau / 8f;
            At(house + new Vector3(0, 0.03f, 0), new Basis(Vector3.Up, a) * new Basis(Vector3.Forward, 0f));
            _kit.Box(new Vector3(0.008f, 0.008f, 0.4f), new Color("f4f1ea"));
        }

        var dome = new MeshKit();
        dome.Blob(new Vector3(0.2f, 0.24f, 0.2f), Colors.White, 9, 0f);
        var glass = new StandardMaterial3D
        {
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            AlbedoColor = new Color(0.8f, 0.95f, 1f, 0.28f),
            Roughness = 0.05f,
            Metallic = 0.2f,
            CullMode = BaseMaterial3D.CullModeEnum.Back
        };
        AddPart(AnimatedPartKind.Static, "Glasshouse", dome, new Transform3D(Basis.Identity, house + new Vector3(0, 0.03f, 0)), glass);

        // Shade trees around the edge.
        for (var i = 0; i < 4; i++)
            Tree(Dir(160 + (i * 50)) * 0.48f, 0.9f, _style.Leaves[i % _style.Leaves.Length], 0.4f + (i * 0.05f));
    }
}
