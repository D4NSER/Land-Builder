using System.Collections.Generic;
using Godot;
using LandBuilder.Domain;

namespace LandBuilder.Presentation;

public enum TreeSpecies
{
    Pine,
    Fir,
    Oak,
    Birch,
    Maple,
    Cherry,
    Palm,
    Acacia,
    Baobab,
    JungleGiant,
    Banana,
    Bamboo,
    Willow,
    BlackPine,
    Cypress,
    Poplar
}

public enum Architecture
{
    European,
    Nordic,
    Japanese,
    Tropical,
    African,
    Rustic
}

/// <summary>How strongly each season shows on an island: some never see snow, some barely turn in autumn.</summary>
public sealed record SeasonProfile(float Snow, float Autumn, float Spring, float Dry);

/// <summary>
/// Visual identity of a world theme: ground palettes, tree species, architecture, sea, sky and seasons. The rules only
/// know the theme's name; everything about how it looks lives here.
/// </summary>
public sealed class ThemeStyle
{
    public required WorldTheme Theme { get; init; }
    public required Dictionary<Terrain, (Color A, Color B)> Ground { get; init; }
    public required (TreeSpecies Species, float Weight)[] ForestTrees { get; init; }
    public required (TreeSpecies Species, float Weight)[] MeadowTrees { get; init; }
    public required (TreeSpecies Species, float Weight)[] VillageTrees { get; init; }
    public required Color[] Leaves { get; init; }
    public required Color[] Conifers { get; init; }
    public required Architecture Architecture { get; init; }
    public required Color[] Roofs { get; init; }
    public required Color[] Walls { get; init; }
    public required Color[] Flowers { get; init; }
    public required Color[] Blossoms { get; init; }
    public required Color Shore { get; init; }
    public required Color LakeShallow { get; init; }
    public required Color LakeDeep { get; init; }
    public required Color OceanShallow { get; init; }
    public required Color OceanDeep { get; init; }
    public required Color ShelfSand { get; init; }
    public required Color ShelfTeal { get; init; }
    public required Color SkyTop { get; init; }
    public required Color SkyHorizon { get; init; }
    public required Color SunColor { get; init; }
    public required float SnowLine { get; init; }
    public required SeasonProfile Seasons { get; init; }
    public bool RicePaddies { get; init; }
    public bool Rainforest { get; init; }

    public Color GroundA(Terrain t) => Ground[t].A;
    public Color GroundB(Terrain t) => Ground[t].B;

    private static Dictionary<Terrain, (Color, Color)> BaseGround() => new()
    {
        [Terrain.Grass] = (new Color("74b956"), new Color("9cc95c")),
        [Terrain.Forest] = (new Color("4a8a43"), new Color("3a733a")),
        [Terrain.Field] = (new Color("ecc65a"), new Color("d9a843")),
        [Terrain.Village] = (new Color("aecb7a"), new Color("c9cf8f")),
        [Terrain.Water] = (new Color("8cb8a0"), new Color("5f8f8c")),
        [Terrain.Mountain] = (new Color("9b968c"), new Color("7e7a74")),
        [Terrain.Sand] = (new Color("efd79b"), new Color("e2c07e")),
        [Terrain.Snow] = (new Color("f3f7fb"), new Color("dce7f0")),
        [Terrain.Swamp] = (new Color("6f8248"), new Color("566a3b")),
        [Terrain.Orchard] = (new Color("94c466"), new Color("a9d273")),
        [Terrain.River] = (new Color("8fb59a"), new Color("6f9e8f"))
    };

    private static Dictionary<Terrain, (Color, Color)> With(params (Terrain T, string A, string B)[] overrides)
    {
        var g = BaseGround();
        foreach (var (t, a, b) in overrides)
            g[t] = (new Color(a), new Color(b));
        return g;
    }

    private static readonly Dictionary<WorldTheme, ThemeStyle> Styles = new()
    {
        [WorldTheme.Temperate] = new ThemeStyle
        {
            Theme = WorldTheme.Temperate,
            Ground = BaseGround(),
            ForestTrees = [(TreeSpecies.Pine, 4f), (TreeSpecies.Fir, 2f), (TreeSpecies.Oak, 3f), (TreeSpecies.Birch, 1f), (TreeSpecies.Maple, 1f)],
            MeadowTrees = [(TreeSpecies.Oak, 3f), (TreeSpecies.Poplar, 1.5f), (TreeSpecies.Birch, 1f)],
            VillageTrees = [(TreeSpecies.Oak, 2f), (TreeSpecies.Poplar, 2f), (TreeSpecies.Cherry, 0.5f)],
            Leaves = [new("5aa847"), new("6fb851"), new("4c9a42")],
            Conifers = [new("2e7a45"), new("246338")],
            Architecture = Architecture.European,
            Roofs = [new("d0563f"), new("d0563f"), new("5a7896"), new("e08a3f"), new("8b5a3c")],
            Walls = [new("f4ecdc"), new("eadcc0"), new("f6e7c8")],
            Flowers = [new("f6f1e3"), new("f2c94c"), new("e86f8a"), new("9b7ede")],
            Blossoms = [new("f7c6d6"), new("fbe4ec"), new("f3a9c0")],
            Shore = new("e3d29d"),
            LakeShallow = new("86d6cc"),
            LakeDeep = new("3a88b4"),
            OceanShallow = new("4fc6c6"),
            OceanDeep = new("1e6aa3"),
            ShelfSand = new("c9dcae"),
            ShelfTeal = new("3f8fa6"),
            SkyTop = new("5aa6dc"),
            SkyHorizon = new("cfe8f2"),
            SunColor = new("fff1dc"),
            SnowLine = 0.62f,
            Seasons = new SeasonProfile(Snow: 0.8f, Autumn: 1f, Spring: 1f, Dry: 0f)
        },
        [WorldTheme.Nordic] = new ThemeStyle
        {
            Theme = WorldTheme.Nordic,
            Ground = With((Terrain.Grass, "6fa46a", "88b57c"), (Terrain.Forest, "3d6b45", "34603d"), (Terrain.Field, "d9bf6a", "c4a65a"),
                (Terrain.Village, "9ebd86", "b3c79a"), (Terrain.Mountain, "8f939a", "74787f"), (Terrain.Orchard, "88b878", "9cc48a")),
            ForestTrees = [(TreeSpecies.Fir, 5f), (TreeSpecies.Pine, 3f), (TreeSpecies.Birch, 2f)],
            MeadowTrees = [(TreeSpecies.Birch, 3f), (TreeSpecies.Fir, 1f)],
            VillageTrees = [(TreeSpecies.Birch, 2f), (TreeSpecies.Fir, 1f)],
            Leaves = [new("6f9e5a"), new("c9b64a"), new("7fa864")],
            Conifers = [new("2a6a4a"), new("1f5640"), new("2f7553")],
            Architecture = Architecture.Nordic,
            Roofs = [new("6b8f4e"), new("5e8246"), new("3f4a52"), new("7a9a52")],
            Walls = [new("b5533c"), new("a84b36"), new("e9e2d4"), new("c9a46a")],
            Flowers = [new("ffffff"), new("b9a3e3"), new("f2d64c")],
            Blossoms = [new("fbe4ec"), new("ffffff")],
            Shore = new("cfc9b0"),
            LakeShallow = new("7cc0c4"),
            LakeDeep = new("2c6f96"),
            OceanShallow = new("47a9b3"),
            OceanDeep = new("1a527e"),
            ShelfSand = new("b9c9b4"),
            ShelfTeal = new("357f96"),
            SkyTop = new("6fa8d8"),
            SkyHorizon = new("e2eef3"),
            SunColor = new("f4f6ff"),
            SnowLine = 0.38f,
            Seasons = new SeasonProfile(Snow: 1f, Autumn: 0.8f, Spring: 0.6f, Dry: 0f)
        },
        [WorldTheme.Tropical] = new ThemeStyle
        {
            Theme = WorldTheme.Tropical,
            Ground = With((Terrain.Grass, "6fc757", "9bd85d"), (Terrain.Forest, "357f3e", "2a6e37"), (Terrain.Sand, "f6e7bd", "efd9a0"),
                (Terrain.Village, "b5d983", "d2dd96"), (Terrain.Swamp, "5f8a4a", "4a7440")),
            ForestTrees = [(TreeSpecies.JungleGiant, 3f), (TreeSpecies.Oak, 2f), (TreeSpecies.Palm, 2f), (TreeSpecies.Banana, 2f)],
            MeadowTrees = [(TreeSpecies.Palm, 3f), (TreeSpecies.Banana, 1f)],
            VillageTrees = [(TreeSpecies.Palm, 3f), (TreeSpecies.Banana, 2f)],
            Leaves = [new("3f9a46"), new("5cb84e"), new("2f8a52"), new("74c35a")],
            Conifers = [new("2e7a45")],
            Architecture = Architecture.Tropical,
            Roofs = [new("d9b56a"), new("c9a35a"), new("e0c27a")],
            Walls = [new("c9a070"), new("d8b585"), new("b98d5a")],
            Flowers = [new("ff6f91"), new("ffc75f"), new("f9f871"), new("ff9671")],
            Blossoms = [new("ff8fab"), new("ffc2d1")],
            Shore = new("f3e4b8"),
            LakeShallow = new("6fe2d2"),
            LakeDeep = new("1f95b8"),
            OceanShallow = new("3fe0cf"),
            OceanDeep = new("1180b5"),
            ShelfSand = new("f0e9c4"),
            ShelfTeal = new("33b5b8"),
            SkyTop = new("3f9fe0"),
            SkyHorizon = new("c6eef6"),
            SunColor = new("fff4e0"),
            SnowLine = 0.7f,
            Seasons = new SeasonProfile(Snow: 0f, Autumn: 0.15f, Spring: 0.8f, Dry: 0.5f),
            Rainforest = true
        },
        [WorldTheme.Autumn] = new ThemeStyle
        {
            Theme = WorldTheme.Autumn,
            Ground = With((Terrain.Grass, "a5b45a", "c3b75e"), (Terrain.Forest, "7a7a3c", "6a5f34"), (Terrain.Field, "e9b44f", "d4913e"),
                (Terrain.Village, "b8bc78", "cfc58a"), (Terrain.Orchard, "a6b862", "bcc56e")),
            ForestTrees = [(TreeSpecies.Maple, 4f), (TreeSpecies.Oak, 3f), (TreeSpecies.Pine, 2f), (TreeSpecies.Birch, 1f)],
            MeadowTrees = [(TreeSpecies.Maple, 2f), (TreeSpecies.Oak, 2f), (TreeSpecies.Poplar, 1f)],
            VillageTrees = [(TreeSpecies.Maple, 2f), (TreeSpecies.Oak, 1f)],
            Leaves = [new("d9682b"), new("e8a33a"), new("b8452e"), new("c9b23a"), new("e07b39")],
            Conifers = [new("3f6b3f"), new("355c38")],
            Architecture = Architecture.Rustic,
            Roofs = [new("6b4a35"), new("5a5f6b"), new("8b3a2a"), new("a0522d")],
            Walls = [new("efe3c8"), new("e0cfa8"), new("b5533c")],
            Flowers = [new("e8a33a"), new("b8452e"), new("f2d64c")],
            Blossoms = [new("d64545"), new("e66b3a"), new("c93a3a")],
            Shore = new("dccb98"),
            LakeShallow = new("86c9bb"),
            LakeDeep = new("3a7f9e"),
            OceanShallow = new("4cb5b6"),
            OceanDeep = new("1f5f93"),
            ShelfSand = new("cfd3a8"),
            ShelfTeal = new("3e8a9c"),
            SkyTop = new("6aa2d2"),
            SkyHorizon = new("f1e3c9"),
            SunColor = new("ffe2bf"),
            SnowLine = 0.62f,
            Seasons = new SeasonProfile(Snow: 0.6f, Autumn: 1f, Spring: 0.7f, Dry: 0f)
        },
        [WorldTheme.Savanna] = new ThemeStyle
        {
            Theme = WorldTheme.Savanna,
            Ground = With((Terrain.Grass, "bdbb5f", "d3c26c"), (Terrain.Forest, "7f913f", "6e8238"), (Terrain.Field, "e6c15a", "d1a446"),
                (Terrain.Village, "c9c07e", "dacb92"), (Terrain.Sand, "eccb92", "deb578"), (Terrain.Mountain, "b08f74", "967860"), (Terrain.Orchard, "a8b968", "bcc678")),
            ForestTrees = [(TreeSpecies.Acacia, 4f), (TreeSpecies.Baobab, 1.5f), (TreeSpecies.Oak, 1f)],
            MeadowTrees = [(TreeSpecies.Acacia, 3f), (TreeSpecies.Baobab, 1f)],
            VillageTrees = [(TreeSpecies.Acacia, 2f), (TreeSpecies.Baobab, 1f)],
            Leaves = [new("7d9a3c"), new("94a845"), new("6f8a35")],
            Conifers = [new("4f7a3f")],
            Architecture = Architecture.African,
            Roofs = [new("d9b56a"), new("c9a35a"), new("b8924a")],
            Walls = [new("c9673b"), new("d48b4a"), new("e3c49a"), new("b85a32")],
            Flowers = [new("f2c94c"), new("e86f3a"), new("ffffff")],
            Blossoms = [new("f5a623"), new("f7c35f")],
            Shore = new("e8d29c"),
            LakeShallow = new("82cfc0"),
            LakeDeep = new("2f80a6"),
            OceanShallow = new("4cc2c0"),
            OceanDeep = new("1c66a0"),
            ShelfSand = new("e2d6aa"),
            ShelfTeal = new("3c95a8"),
            SkyTop = new("62aade"),
            SkyHorizon = new("f4e8cc"),
            SunColor = new("fff0d4"),
            SnowLine = 0.72f,
            Seasons = new SeasonProfile(Snow: 0f, Autumn: 0.3f, Spring: 0.6f, Dry: 1f)
        },
        [WorldTheme.Japanese] = new ThemeStyle
        {
            Theme = WorldTheme.Japanese,
            Ground = With((Terrain.Grass, "7fbf5e", "9ccb68"), (Terrain.Forest, "3f7a45", "356b3d"), (Terrain.Field, "9cc96a", "b7d27a"),
                (Terrain.Village, "b9c98e", "d1d3a6"), (Terrain.Orchard, "9fcf7a", "b5d98a"), (Terrain.Mountain, "8f8c88", "76736f")),
            ForestTrees = [(TreeSpecies.BlackPine, 3f), (TreeSpecies.Bamboo, 2.5f), (TreeSpecies.Maple, 2f), (TreeSpecies.Cypress, 1.5f), (TreeSpecies.Cherry, 1f)],
            MeadowTrees = [(TreeSpecies.Cherry, 3f), (TreeSpecies.BlackPine, 1.5f), (TreeSpecies.Maple, 1f)],
            VillageTrees = [(TreeSpecies.Cherry, 3f), (TreeSpecies.BlackPine, 2f), (TreeSpecies.Bamboo, 1f)],
            Leaves = [new("4f9a4a"), new("63a852"), new("d6452f")],
            Conifers = [new("2f6b48"), new("275c3e")],
            Architecture = Architecture.Japanese,
            Roofs = [new("4a5566"), new("3d4757"), new("5b6474"), new("8b3a2a")],
            Walls = [new("f4efe4"), new("efe6d2")],
            Flowers = [new("f7c6d6"), new("ffffff"), new("b9a3e3")],
            Blossoms = [new("f9c3d5"), new("fbd9e5"), new("f4a7c0")],
            Shore = new("ddd2a8"),
            LakeShallow = new("7fd0c6"),
            LakeDeep = new("2f7fa8"),
            OceanShallow = new("4fbfc4"),
            OceanDeep = new("1d5f99"),
            ShelfSand = new("cfdcb6"),
            ShelfTeal = new("3a8ca0"),
            SkyTop = new("6aaee0"),
            SkyHorizon = new("f3e6ea"),
            SunColor = new("fff3ea"),
            SnowLine = 0.48f,
            Seasons = new SeasonProfile(Snow: 0.75f, Autumn: 1f, Spring: 1.2f, Dry: 0f),
            RicePaddies = true
        }
    };

    public static ThemeStyle For(WorldTheme theme) => Styles[theme];
}
