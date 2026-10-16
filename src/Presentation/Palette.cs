using Godot;
using LandBuilder.Domain;

namespace LandBuilder.Presentation;

/// <summary>Colour language shared by terrain, props and UI so every surface reads as one world.</summary>
public static class Palette
{
    // UI
    public static readonly Color Ink = new("1f2a33");
    public static readonly Color InkSoft = new("4a5862");
    public static readonly Color Paper = new("fbf6ec");
    public static readonly Color PaperShade = new("efe6d4");
    public static readonly Color Accent = new("f2a541");
    public static readonly Color AccentDeep = new("d9822b");
    public static readonly Color Good = new("6cc070");
    public static readonly Color Bad = new("e8665a");
    public static readonly Color Sky = new("8fc9e8");
    public static readonly Color Gold = new("ffd25e");

    public static Color TerrainBase(Terrain terrain) => terrain switch
    {
        Terrain.Grass => new Color("8fcf5a"),
        Terrain.Forest => new Color("4f9a4c"),
        Terrain.Field => new Color("e9c25a"),
        Terrain.Village => new Color("b9c98a"),
        Terrain.Water => new Color("6c9fa8"),
        Terrain.Mountain => new Color("9a978f"),
        Terrain.Sand => new Color("ecd49a"),
        _ => Colors.Magenta
    };

    /// <summary>Saturated swatch for icons, quest chips and edge markers.</summary>
    public static Color TerrainSwatch(Terrain terrain) => terrain switch
    {
        Terrain.Grass => new Color("79c447"),
        Terrain.Forest => new Color("2f7d3b"),
        Terrain.Field => new Color("e8b23a"),
        Terrain.Village => new Color("d8674b"),
        Terrain.Water => new Color("3d9ad1"),
        Terrain.Mountain => new Color("7d7a86"),
        Terrain.Sand => new Color("e0b56a"),
        _ => Colors.Magenta
    };

    public static string TerrainGlyph(Terrain terrain) => terrain switch
    {
        Terrain.Grass => "❀",
        Terrain.Forest => "♣",
        Terrain.Field => "≋",
        Terrain.Village => "⌂",
        Terrain.Water => "≈",
        Terrain.Mountain => "▲",
        Terrain.Sand => "◠",
        _ => "?"
    };

    public static readonly Color SoilTop = new("7a5638");
    public static readonly Color SoilMid = new("634530");
    public static readonly Color Rock = new("575049");
    public static readonly Color RockDeep = new("3f3a36");

    public static readonly Color TrunkBrown = new("7b5537");
    public static readonly Color PineGreen = new("2e7a45");
    public static readonly Color PineGreenDark = new("246338");
    public static readonly Color LeafGreen = new("5aa847");
    public static readonly Color LeafGreenLight = new("7cc456");
    public static readonly Color AutumnOrange = new("e08a3a");
    public static readonly Color Wall = new("f4ecdc");
    public static readonly Color WallWarm = new("eadcc0");
    public static readonly Color RoofRed = new("d0563f");
    public static readonly Color RoofBlue = new("5a7896");
    public static readonly Color RoofOrange = new("e08a3f");
    public static readonly Color Wheat = new("f0cf6a");
    public static readonly Color Hay = new("dfb455");
    public static readonly Color Stone = new("a9a59c");
    public static readonly Color StoneDark = new("807b73");
    public static readonly Color Snow = new("f4f7fa");
    public static readonly Color Cactus = new("5e9e58");
    public static readonly Color Lily = new("6fb35a");
}
