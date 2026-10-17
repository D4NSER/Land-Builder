using System.Collections.Generic;
using System.Linq;
using Godot;
using LandBuilder.Domain;

namespace LandBuilder.Presentation;

/// <summary>
/// The parts of a finished wonder that reach out over its plots, so a landmark reads as one structure spanning
/// several tiles rather than a big model on one: curtain walls and towers, piers, gardens, hangars... Built in a
/// world-aligned, unscaled frame centred on the footprint.
/// </summary>
public sealed partial class WonderModelBuilder
{
    private static readonly Color PathColor = new("dccba3");

    /// <summary>Dressing on and between the plots of a finished wonder.</summary>
    public ArrayMesh BuildSurroundings(WonderType type, IReadOnlyList<(Vector3 Offset, WonderCell Rule)> plots)
    {
        // Each plot gets an "outer" spot past the central landmark, facing away start it.
        var outer = plots
            .Select(p =>
            {
                var flat = p.Offset with { Y = 0 };
                var dir = flat.LengthSquared() < 0.01f ? Vector3.Right : flat.Normalized();
                return (Spot: flat + (dir * 0.42f), Dir: dir, p.Rule.Terrain);
            })
            .OrderBy(o => Mathf.Atan2(-o.Dir.Z, o.Dir.X))
            .ToList();

        // Gravel paths from the landmark out to every land plot tie the footprint together.
        foreach (var (spot, dir, terrain) in outer)
        {
            if (terrain != Terrain.Water)
                Path(dir * 0.55f, spot + (dir * 0.25f), 0.07f);
        }

        if (type == WonderType.Castle)
            CurtainWall(outer.Select(o => o.Spot).ToList());

        for (var i = 0; i < outer.Count; i++)
        {
            var (spot, dir, terrain) = outer[i];
            var side = new Vector3(-dir.Z, 0, dir.X);
            var yaw = Mathf.Atan2(-dir.Z, dir.X);
            switch (type, terrain)
            {
                case (WonderType.Castle, Terrain.Village):
                case (WonderType.Cathedral, _):
                case (WonderType.Harbour, Terrain.Village):
                    TownRow(spot + (dir * 0.18f), side, yaw, type == WonderType.Harbour ? 2 : 3, i);
                    break;
                case (WonderType.Castle, Terrain.Mountain):
                    RoundTower(spot + (dir * 0.25f), 0.07f, 0.42f, Stone, Slate, 0.6f);
                    Flag(OnGround(spot + (dir * 0.25f)) + new Vector3(0, 0.62f, 0), new Color("c4452f"), 0.9f);
                    break;
                case (WonderType.GreatPagoda, Terrain.Village):
                    Torii(spot + (dir * 0.15f), yaw);
                    Lanterns(spot - (dir * 0.2f), side, 4);
                    break;
                case (WonderType.GreatPagoda, Terrain.Forest):
                case (WonderType.BotanicalPark, Terrain.Forest):
                    Grove(spot + (dir * 0.1f), type == WonderType.GreatPagoda, i);
                    break;
                case (WonderType.Harbour, Terrain.Water):
                case (WonderType.TallShip, Terrain.Water):
                    Pier(spot - (dir * 0.45f), dir, type == WonderType.Harbour ? 0.85f : 0.55f, i);
                    break;
                case (WonderType.Lighthouse, Terrain.Water):
                    Buoys(spot, side);
                    break;
                case (WonderType.RocketPad, Terrain.Field):
                    Hangar(spot + (dir * 0.1f), yaw);
                    break;
                case (WonderType.RocketPad, Terrain.Grass):
                    RadarDish(spot + (dir * 0.15f));
                    break;
                case (WonderType.Observatory, Terrain.Mountain):
                    MiniDome(spot + (dir * 0.1f), i);
                    break;
                case (WonderType.BotanicalPark, Terrain.Grass):
                    FlowerBeds(spot, side, i);
                    break;
                case (WonderType.BotanicalPark, Terrain.Water):
                    Gazebo(spot - (dir * 0.15f));
                    break;
            }
        }

        return _kit.Commit(withCustom: true);
    }

    // ---------------------------------------------------------------- dressing helpers

    private void Path(Vector3 start, Vector3 end, float width)
    {
        var d = end - start;
        var steps = Mathf.Max(2, Mathf.CeilToInt(d.Length() / 0.07f));
        var yaw = Mathf.Atan2(-d.Z, d.X);
        for (var i = 0; i < steps; i++)
        {
            var p = OnGround(start.Lerp(end, (i + 0.5f) / steps));
            if (p.Y < _water + 0.005f)
                continue;
            Stage(p, 0.02f + (i * 0.01f), Sway.Rigid);
            At(p - new Vector3(0, 0.012f, 0), yaw);
            _kit.Box(new Vector3(d.Length() / steps * 1.05f, 0.018f, width), PathColor);
        }
    }

    /// <summary>Towers on the outer spot of every plot, joined by crenellated walls.</summary>
    private void CurtainWall(List<Vector3> spots)
    {
        for (var i = 0; i < spots.Count; i++)
        {
            var a = spots[i];
            var b = spots[(i + 1) % spots.Count];
            Wall(OnGround(a), OnGround(b), 0.16f, 0.05f, Stone, 0.3f + (i * 0.05f));
        }

        for (var i = 0; i < spots.Count; i++)
        {
            RoundTower(spots[i], 0.085f, 0.3f, Stone, Slate, 0.45f + (i * 0.05f));
            Flag(OnGround(spots[i]) + new Vector3(0, 0.45f, 0), new Color("c4452f"), 0.9f);
        }
    }

    private void Townhouse(Vector3 at, float yaw, int seed)
    {
        Color[] walls = [Palette.Wall, Palette.WallWarm, new("e8d9c0"), new("f0e2cf")];
        Color[] roofs = [new("a8452f"), new("4a5a6e"), new("8a5a3a"), new("b5533c")];
        var p = OnGround(at);
        var h = 0.12f + ((seed % 3) * 0.04f);
        Stage(p, 0.35f + ((seed % 4) * 0.05f));
        At(p - new Vector3(0, 0.02f, 0), yaw);
        _kit.Box(new Vector3(0.12f, h, 0.1f), walls[seed % walls.Length]);
        _kit.Sway = Sway.Roof;
        At(p + new Vector3(0, h - 0.02f, 0), yaw);
        _kit.GableRoof(0.14f, 0.12f, 0.07f, roofs[seed % roofs.Length], walls[seed % walls.Length]);
    }

    private void TownRow(Vector3 centre, Vector3 side, float yaw, int count, int seed)
    {
        for (var k = 0; k < count; k++)
        {
            var t = k - ((count - 1) / 2f);
            Townhouse(centre + (side * t * 0.17f), yaw + (Mathf.Pi / 2f), seed * 7 + k);
        }
    }

    private void Torii(Vector3 at, float yaw)
    {
        var p = OnGround(at);
        var red = new Color("c8402e");
        Stage(p, 0.4f);
        var side = new Vector3(Mathf.Sin(yaw), 0, Mathf.Cos(yaw));
        foreach (var s in new[] { -1f, 1f })
        {
            At(p + (side * s * 0.09f) - new Vector3(0, 0.02f, 0));
            _kit.Frustum(6, 0.014f, 0.012f, 0.22f, red);
        }

        At(p + new Vector3(0, 0.19f, 0), yaw + (Mathf.Pi / 2f));
        _kit.Box(new Vector3(0.24f, 0.022f, 0.03f), red);
        At(p + new Vector3(0, 0.15f, 0), yaw + (Mathf.Pi / 2f));
        _kit.Box(new Vector3(0.2f, 0.014f, 0.02f), red.Darkened(0.2f));
    }

    private void Lanterns(Vector3 centre, Vector3 side, int count)
    {
        for (var k = 0; k < count; k++)
        {
            var p = OnGround(centre + (side * (k - ((count - 1) / 2f)) * 0.12f));
            Stage(p, 0.5f + (k * 0.04f), Sway.Rigid);
            At(p - new Vector3(0, 0.01f, 0));
            _kit.Frustum(6, 0.018f, 0.012f, 0.05f, Stone);
            At(p + new Vector3(0, 0.04f, 0));
            _kit.Box(new Vector3(0.03f, 0.03f, 0.03f), new Color("ffe3a0"));
            At(p + new Vector3(0, 0.07f, 0));
            _kit.Frustum(6, 0.03f, 0f, 0.025f, StoneDark);
        }
    }

    private void Grove(Vector3 centre, bool blossom, int seed)
    {
        for (var k = 0; k < 5; k++)
        {
            var a = (k * Mathf.Tau / 5f) + seed;
            var p = centre + (new Vector3(Mathf.Cos(a), 0, -Mathf.Sin(a)) * (k == 0 ? 0f : 0.2f));
            var leaf = blossom ? (k % 2 == 0 ? new Color("f9c3d5") : new Color("fbd9e5")) : _style.Leaves[k % _style.Leaves.Length];
            Tree(p, k == 0 ? 1.6f : 1.15f, leaf, 0.3f + (k * 0.05f));
        }
    }

    private void Pier(Vector3 start, Vector3 dir, float length, int seed)
    {
        var deck = _water + 0.03f;
        var end = start + (dir * length);
        var mid = (start + end) / 2f;
        var yaw = Mathf.Atan2(-dir.Z, dir.X);
        Stage(mid with { Y = deck }, 0.25f, Sway.Rigid);
        At(mid with { Y = deck }, yaw);
        _kit.Box(new Vector3(length, 0.014f, 0.09f), Wood);
        for (var k = 0; k <= 6; k++)
        {
            var pile = start.Lerp(end, k / 6f);
            foreach (var s in new[] { -1f, 1f })
            {
                At(new Vector3(pile.X, deck - 0.14f, pile.Z) + (new Vector3(-dir.Z, 0, dir.X) * s * 0.04f));
                _kit.Box(new Vector3(0.014f, 0.15f, 0.014f), Wood.Darkened(0.25f));
            }
        }

        // Crates at the land end, a rowing boat tied up at the far end.
        for (var k = 0; k < 3; k++)
        {
            At(start with { Y = deck + 0.012f } + (dir * (0.05f + (k * 0.05f))), yaw + k);
            _kit.Box(new Vector3(0.035f, 0.035f, 0.035f), k % 2 == 0 ? new Color("c8a070") : new Color("8b5a3b"));
        }

        var boat = new MeshKit();
        boat.Box(new Vector3(0.16f, 0.035f, 0.06f), new Color("8b5a3b"), new Color("a07a50"));
        boat.Transform = new Transform3D(Basis.Identity, new Vector3(0.08f, 0, 0));
        boat.Frustum(3, 0.03f, 0f, 0.035f, new Color("8b5a3b"));
        var side = new Vector3(-dir.Z, 0, dir.X) * (seed % 2 == 0 ? 0.11f : -0.11f);
        AddPart(AnimatedPartKind.Ship, "Rowboat", boat, new Transform3D(new Basis(Vector3.Up, yaw), (end - (dir * 0.12f) + side) with { Y = _water + 0.002f }));
    }

    private void Buoys(Vector3 centre, Vector3 side)
    {
        for (var k = 0; k < 2; k++)
        {
            var p = (centre + (side * (k == 0 ? -0.25f : 0.25f))) with { Y = _water - 0.02f };
            Stage(p, 0.4f, Sway.Rigid);
            At(p);
            _kit.Frustum(8, 0.035f, 0.025f, 0.06f, k == 0 ? new Color("c4452f") : Colors.White);
            At(p + new Vector3(0, 0.06f, 0));
            _kit.Frustum(8, 0.01f, 0.01f, 0.05f, new Color("3a3a3a"));
            At(p + new Vector3(0, 0.11f, 0));
            _kit.Box(new Vector3(0.02f, 0.02f, 0.02f), new Color("ffe7a3"));
        }
    }

    private void Hangar(Vector3 at, float yaw)
    {
        var p = OnGround(at);
        Stage(p, 0.3f);
        At(p - new Vector3(0, 0.02f, 0), yaw);
        _kit.Box(new Vector3(0.36f, 0.12f, 0.24f), new Color("d6d2c8"));
        _kit.Sway = Sway.Roof;
        At(p + new Vector3(0, 0.1f, 0), new Basis(Vector3.Up, yaw) * new Basis(Vector3.Right, Mathf.Pi / 2f));
        _kit.Frustum(10, 0.13f, 0.13f, 0.36f, new Color("9aa4ad"));
        At(p + new Vector3(0, 0.0f, 0) + (new Vector3(Mathf.Cos(yaw), 0, -Mathf.Sin(yaw)) * 0.181f), yaw);
        _kit.Box(new Vector3(0.004f, 0.09f, 0.14f), new Color("3a3f45"));
        for (var k = 0; k < 2; k++)
        {
            var tank = OnGround(at + new Vector3(Mathf.Sin(yaw), 0, Mathf.Cos(yaw)) * (0.22f + (k * 0.11f)));
            Stage(tank, 0.4f);
            At(tank - new Vector3(0, 0.02f, 0));
            _kit.Frustum(10, 0.045f, 0.045f, 0.16f, new Color("f4f1ea"));
            At(tank + new Vector3(0, 0.14f, 0));
            _kit.Blob(new Vector3(0.045f, 0.025f, 0.045f), new Color("f4f1ea"), 3, 0.01f);
        }
    }

    private void RadarDish(Vector3 at)
    {
        var p = OnGround(at);
        Stage(p, 0.4f);
        At(p - new Vector3(0, 0.02f, 0));
        _kit.Frustum(8, 0.05f, 0.04f, 0.06f, new Color("d6d2c8"));
        At(p + new Vector3(0, 0.04f, 0));
        _kit.Box(new Vector3(0.02f, 0.18f, 0.02f), new Color("c9c4bb"));
        At(p + new Vector3(0, 0.21f, 0), new Basis(Vector3.Forward, 0.7f));
        _kit.Frustum(14, 0.012f, 0.13f, 0.05f, new Color("f4f1ea"));
    }

    private void MiniDome(Vector3 at, int seed)
    {
        var p = OnGround(at);
        Stage(p, 0.35f);
        At(p - new Vector3(0, 0.02f, 0));
        _kit.Frustum(12, 0.08f, 0.08f, 0.08f, new Color("f4f1ea"));
        _kit.Sway = Sway.Roof;
        At(p + new Vector3(0, 0.06f, 0));
        _kit.Blob(new Vector3(0.085f, 0.075f, 0.085f), new Color("e8eef2"), seed, 0f);
        var mast = OnGround(at + new Vector3(0.14f, 0, 0.06f));
        Stage(mast, 0.5f, Sway.Rigid);
        At(mast);
        _kit.Box(new Vector3(0.012f, 0.28f, 0.012f), new Color("c9c4bb"));
        At(mast + new Vector3(0, 0.28f, 0));
        _kit.Box(new Vector3(0.02f, 0.02f, 0.02f), new Color("e8553f"));
    }

    private void FlowerBeds(Vector3 centre, Vector3 side, int seed)
    {
        Color[] beds = [new("e86f8a"), new("f2c94c"), new("9b7ede"), new("ff9671")];
        for (var k = 0; k < 3; k++)
        {
            var p = OnGround(centre + (side * (k - 1) * 0.16f));
            Stage(p, 0.3f + (k * 0.05f), Sway.Flower);
            At(p, k);
            _kit.Blob(new Vector3(0.07f, 0.025f, 0.05f), beds[(seed + k) % beds.Length], k, 0.2f);
        }
    }

    private void Gazebo(Vector3 at)
    {
        var p = OnGround(at);
        var floor = Mathf.Max(p.Y, _water + 0.02f);
        Stage(p, 0.4f);
        At(p with { Y = floor - 0.02f });
        _kit.Frustum(8, 0.09f, 0.09f, 0.03f, Palette.Wall);
        for (var k = 0; k < 6; k++)
        {
            var a = k * Mathf.Tau / 6f;
            At(p with { Y = floor } + new Vector3(Mathf.Cos(a) * 0.075f, 0, Mathf.Sin(a) * 0.075f));
            _kit.Box(new Vector3(0.01f, 0.1f, 0.01f), Colors.White);
        }

        _kit.Sway = Sway.Roof;
        At(p with { Y = floor + 0.1f });
        _kit.Frustum(8, 0.11f, 0f, 0.07f, new Color("5aa0a8"));
    }

    // ---------------------------------------------------------------- tall ship

    /// <summary>A three-masted galleon, built along +X, riding in the basin dug for it.</summary>
    private void BuildGalleon()
    {
        var hull = new Color("5a3a2a");
        var hullLight = new Color("7a5038");
        var deck = new Color("b08a5c");
        var trim = Gold;
        var sail = new Color("f6efe0");
        var ship = new MeshKit();

        void Box(Vector3 at, Vector3 size, Color color, Color? top = null, Basis? basis = null)
        {
            ship.Transform = new Transform3D(basis ?? Basis.Identity, at);
            ship.Box(size, color, top ?? color);
        }

        // Hull: stacked, tapering slabs suggest a rounded belly; the waterline sits a third of the way up.
        Box(new Vector3(0, -0.34f, 0), new Vector3(1.3f, 0.1f, 0.2f), hull);
        Box(new Vector3(0, -0.26f, 0), new Vector3(1.7f, 0.12f, 0.38f), hull);
        Box(new Vector3(0, -0.15f, 0), new Vector3(2.0f, 0.2f, 0.5f), hullLight, deck);
        Box(new Vector3(0, 0.02f, 0), new Vector3(2.02f, 0.03f, 0.51f), trim);

        // Bow wedge and bowsprit.
        ship.Transform = new Transform3D(new Basis(Vector3.Back, -Mathf.Pi / 2f).Scaled(new Vector3(1f, 1f, 1f)), new Vector3(1.0f, -0.06f, 0));
        ship.Frustum(4, 0.25f, 0.02f, 0.42f, hullLight, twist: Mathf.Pi / 4f);
        Box(new Vector3(1.25f, 0.08f, 0), new Vector3(0.7f, 0.03f, 0.03f), Wood, basis: new Basis(Vector3.Back, 0.32f));

        // Fore- and sterncastles with gilded rails.
        Box(new Vector3(0.78f, 0.05f, 0), new Vector3(0.36f, 0.1f, 0.46f), hullLight, deck);
        Box(new Vector3(-0.78f, 0.05f, 0), new Vector3(0.48f, 0.24f, 0.5f), hullLight, deck);
        Box(new Vector3(-0.78f, 0.29f, 0), new Vector3(0.48f, 0.025f, 0.5f), trim);
        Box(new Vector3(-1.03f, 0.05f, 0), new Vector3(0.03f, 0.2f, 0.4f), trim);
        for (var k = 0; k < 3; k++)
            Box(new Vector3(-1.04f, 0.12f, (k - 1) * 0.12f), new Vector3(0.02f, 0.06f, 0.06f), new Color("ffe7a3"));

        // Gun ports along both sides.
        for (var k = 0; k < 7; k++)
            Box(new Vector3(-0.5f + (k * 0.17f), -0.06f, 0), new Vector3(0.05f, 0.045f, 0.515f), new Color("2a1d16"));

        // Masts, yards and three tiers of sails each.
        foreach (var (x, height, width) in new[] { (-0.45f, 1.15f, 0.62f), (0.15f, 1.38f, 0.72f), (0.62f, 1.05f, 0.56f) })
        {
            Box(new Vector3(x, 0.02f, 0), new Vector3(0.045f, height, 0.045f), Wood);
            for (var tier = 0; tier < 3; tier++)
            {
                var y = 0.2f + (tier * height * 0.27f);
                var w = width * (1f - (tier * 0.2f));
                var h = height * 0.22f;
                Box(new Vector3(x, y + h, 0), new Vector3(0.028f, 0.028f, w + 0.08f), Wood);
                Box(new Vector3(x + 0.02f, y, 0), new Vector3(0.03f, h, w), sail);
            }

            Box(new Vector3(x, 0.02f + height, 0), new Vector3(0.06f, 0.035f, 0.06f), Wood);
            Box(new Vector3(x + 0.07f, height - 0.02f, 0), new Vector3(0.12f, 0.06f, 0.006f), new Color("c4452f"));
        }

        // Rigging from the main mast top down to bow and stern.
        foreach (var (start, end) in new[] { (new Vector3(0.15f, 1.4f, 0), new Vector3(1.4f, 0.28f, 0)), (new Vector3(0.15f, 1.4f, 0), new Vector3(-1.0f, 0.3f, 0)), (new Vector3(-0.45f, 1.17f, 0), new Vector3(-1.0f, 0.3f, 0)) })
        {
            var d = end - start;
            var angle = Mathf.Atan2(d.Y, d.X);
            Box(start + (d / 2f) - new Vector3(0, 0.005f, 0), new Vector3(d.Length(), 0.008f, 0.008f), new Color("3a2f28"), basis: new Basis(Vector3.Back, angle));
        }

        AddPart(AnimatedPartKind.Ship, "Galleon", ship, new Transform3D(Basis.Identity, new Vector3(0, _water + 0.06f, 0)));
    }
}
