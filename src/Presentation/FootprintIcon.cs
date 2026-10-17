using System.Collections.Generic;
using System.Linq;
using Godot;
using LandBuilder.Domain;

namespace LandBuilder.Presentation;

/// <summary>
/// A tiny map of a wonder's footprint for its card: one hex per plot, coloured by the terrain the plot asks for,
/// with the minimum count written on it.
/// </summary>
public partial class FootprintIcon : Control
{
    private readonly List<(Vector2 Centre, WonderCell Rule)> _cells = new();
    private float _radius;

    public FootprintIcon(WonderDef def, float size)
    {
        CustomMinimumSize = new Vector2(size, size);
        SizeFlagsVertical = SizeFlags.ShrinkCenter;
        MouseFilter = MouseFilterEnum.Ignore;

        // Same layout maths as the board (pointy-top axial), squeezed into the icon.
        var points = new List<Vector2>();
        foreach (var cell in def.Cells)
        {
            var coord = new HexCoord(0, 0);
            foreach (var step in cell.Steps)
                coord += HexGrid.Direction(step);
            points.Add(new Vector2(Mathf.Sqrt(3f) * (coord.Q + (coord.R * 0.5f)), 1.5f * coord.R));
        }

        var min = new Vector2(points.Min(p => p.X) - 0.9f, points.Min(p => p.Y) - 1f);
        var max = new Vector2(points.Max(p => p.X) + 0.9f, points.Max(p => p.Y) + 1f);
        var extent = max - min;
        var scale = size / Mathf.Max(extent.X, extent.Y);
        var offset = (new Vector2(size, size) - (extent * scale)) / 2f;
        _radius = scale * 0.98f;
        for (var i = 0; i < points.Count; i++)
            _cells.Add((offset + ((points[i] - min) * scale), def.Cells[i]));
    }

    public override void _Draw()
    {
        var font = UiKit.Black;
        foreach (var (centre, rule) in _cells)
        {
            var corners = new Vector2[6];
            for (var k = 0; k < 6; k++)
            {
                var a = Mathf.DegToRad(30 + (k * 60));
                corners[k] = centre + (new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * _radius * 0.92f);
            }

            DrawColoredPolygon(corners, Palette.TerrainSwatch(rule.Terrain));
            DrawPolyline([.. corners, corners[0]], Colors.White, 1.5f, true);
            var text = $"{rule.Min}";
            var fontSize = Mathf.RoundToInt(_radius * 0.9f);
            var width = font.GetStringSize(text, HorizontalAlignment.Left, -1, fontSize).X;
            DrawString(font, centre + new Vector2(-width / 2f, fontSize * 0.36f), text, HorizontalAlignment.Left, -1, fontSize, Colors.White);
        }
    }
}
