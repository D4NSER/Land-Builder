using System;
using Godot;
using LandBuilder.Domain;

namespace LandBuilder.Presentation;

public enum IconKind
{
    Hex,
    Check,
    Star,
    Streak,
    Flag,
    Mountain,
    Stack,
    Tree,
    Wheat,
    House,
    Waves,
    Dune,
    Flower
}

/// <summary>Small vector icons drawn with canvas primitives, so they look crisp at any scale and need no font glyphs.</summary>
public partial class Icon : Control
{
    private IconKind _kind;
    private Color _color;

    public Icon(IconKind kind, Color color, float size)
    {
        _kind = kind;
        _color = color;
        CustomMinimumSize = new Vector2(size, size);
        MouseFilter = MouseFilterEnum.Ignore;
    }

    public static IconKind ForTerrain(Terrain terrain) => terrain switch
    {
        Terrain.Grass => IconKind.Flower,
        Terrain.Forest => IconKind.Tree,
        Terrain.Field => IconKind.Wheat,
        Terrain.Village => IconKind.House,
        Terrain.Water => IconKind.Waves,
        Terrain.Mountain => IconKind.Mountain,
        Terrain.Sand => IconKind.Dune,
        _ => IconKind.Hex
    };

    public override void _Draw()
    {
        var s = Mathf.Min(Size.X, Size.Y);
        var o = (Size - new Vector2(s, s)) / 2f;
        Vector2 P(float x, float y) => o + new Vector2(x * s, y * s);
        var w = Mathf.Max(1.5f, s * 0.09f);
        var c = _color;

        switch (_kind)
        {
            case IconKind.Hex:
            {
                var pts = new Vector2[6];
                for (var i = 0; i < 6; i++)
                {
                    var a = (Mathf.Pi / 6f) + (i * Mathf.Pi / 3f);
                    pts[i] = P(0.5f + (Mathf.Cos(a) * 0.42f), 0.5f + (Mathf.Sin(a) * 0.42f));
                }

                DrawColoredPolygon(pts, c);
                break;
            }
            case IconKind.Check:
                DrawPolyline([P(0.18f, 0.52f), P(0.42f, 0.75f), P(0.82f, 0.28f)], c, w * 1.4f, true);
                break;
            case IconKind.Star:
            {
                var pts = new Vector2[10];
                for (var i = 0; i < 10; i++)
                {
                    var r = i % 2 == 0 ? 0.45f : 0.2f;
                    var a = (-Mathf.Pi / 2f) + (i * Mathf.Pi / 5f);
                    pts[i] = P(0.5f + (Mathf.Cos(a) * r), 0.53f + (Mathf.Sin(a) * r));
                }

                DrawColoredPolygon(pts, c);
                break;
            }
            case IconKind.Streak:
            {
                // Flame.
                DrawColoredPolygon([P(0.5f, 0.08f), P(0.78f, 0.45f), P(0.74f, 0.75f), P(0.5f, 0.92f), P(0.26f, 0.75f), P(0.24f, 0.48f), P(0.38f, 0.55f)], c);
                DrawColoredPolygon([P(0.5f, 0.45f), P(0.62f, 0.66f), P(0.5f, 0.84f), P(0.38f, 0.68f)], new Color(c, 0.45f));
                break;
            }
            case IconKind.Flag:
                DrawLine(P(0.28f, 0.12f), P(0.28f, 0.9f), c, w, true);
                DrawColoredPolygon([P(0.3f, 0.14f), P(0.8f, 0.3f), P(0.3f, 0.48f)], c);
                break;
            case IconKind.Mountain:
                DrawColoredPolygon([P(0.05f, 0.85f), P(0.4f, 0.22f), P(0.62f, 0.6f), P(0.72f, 0.45f), P(0.95f, 0.85f)], c);
                DrawColoredPolygon([P(0.32f, 0.37f), P(0.4f, 0.22f), P(0.48f, 0.37f), P(0.4f, 0.33f)], new Color(1, 1, 1, 0.85f));
                break;
            case IconKind.Stack:
                for (var i = 0; i < 3; i++)
                {
                    var y = 0.62f - (i * 0.18f);
                    DrawColoredPolygon([P(0.15f, y), P(0.5f, y - 0.12f), P(0.85f, y), P(0.5f, y + 0.12f)], i == 2 ? c : new Color(c, 0.55f + (i * 0.15f)));
                }

                break;
            case IconKind.Tree:
                DrawColoredPolygon([P(0.5f, 0.06f), P(0.8f, 0.5f), P(0.66f, 0.5f), P(0.86f, 0.78f), P(0.14f, 0.78f), P(0.34f, 0.5f), P(0.2f, 0.5f)], c);
                DrawRect(new Rect2(P(0.44f, 0.76f), new Vector2(s * 0.12f, s * 0.16f)), c);
                break;
            case IconKind.Wheat:
                DrawLine(P(0.5f, 0.92f), P(0.5f, 0.3f), c, w, true);
                for (var i = 0; i < 4; i++)
                {
                    var y = 0.22f + (i * 0.14f);
                    DrawColoredPolygon([P(0.5f, y + 0.08f), P(0.28f, y + 0.02f), P(0.38f, y - 0.06f)], c);
                    DrawColoredPolygon([P(0.5f, y + 0.08f), P(0.72f, y + 0.02f), P(0.62f, y - 0.06f)], c);
                }

                DrawCircle(P(0.5f, 0.14f), s * 0.07f, c);
                break;
            case IconKind.House:
                DrawColoredPolygon([P(0.5f, 0.12f), P(0.88f, 0.46f), P(0.12f, 0.46f)], c);
                DrawRect(new Rect2(P(0.22f, 0.44f), new Vector2(s * 0.56f, s * 0.42f)), c);
                DrawRect(new Rect2(P(0.43f, 0.6f), new Vector2(s * 0.14f, s * 0.26f)), new Color(1, 1, 1, 0.6f));
                break;
            case IconKind.Waves:
                for (var row = 0; row < 3; row++)
                {
                    var y = 0.3f + (row * 0.2f);
                    var pts = new Vector2[13];
                    for (var i = 0; i <= 12; i++)
                    {
                        var x = 0.1f + (i / 12f * 0.8f);
                        pts[i] = P(x, y + (Mathf.Sin(i / 12f * Mathf.Tau * 1.5f) * 0.06f));
                    }

                    DrawPolyline(pts, c, w, true);
                }

                break;
            case IconKind.Dune:
                DrawColoredPolygon(Arc(P(0.42f, 0.95f), s * 0.42f, 0.55f, s), c);
                DrawColoredPolygon(Arc(P(0.72f, 0.98f), s * 0.3f, 0.6f, s), new Color(c, 0.75f));
                DrawCircle(P(0.78f, 0.22f), s * 0.1f, new Color(c, 0.9f));
                break;
            case IconKind.Flower:
                for (var i = 0; i < 5; i++)
                {
                    var a = i * Mathf.Tau / 5f;
                    DrawCircle(P(0.5f + (Mathf.Cos(a) * 0.2f), 0.45f + (Mathf.Sin(a) * 0.2f)), s * 0.14f, c);
                }

                DrawCircle(P(0.5f, 0.45f), s * 0.1f, new Color(1f, 0.9f, 0.5f));
                DrawLine(P(0.5f, 0.6f), P(0.5f, 0.92f), c, w, true);
                break;
        }
    }

    private static Vector2[] Arc(Vector2 baseCentre, float radius, float squash, float size)
    {
        var pts = new Vector2[17];
        for (var i = 0; i <= 16; i++)
        {
            var a = Mathf.Pi + (i / 16f * Mathf.Pi);
            pts[i] = baseCentre + new Vector2(Mathf.Cos(a) * radius, Mathf.Sin(a) * radius * squash);
        }

        return pts;
    }
}
