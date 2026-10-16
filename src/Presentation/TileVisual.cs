using System.Collections.Generic;
using Godot;
using LandBuilder.Domain;

namespace LandBuilder.Presentation;

/// <summary>Scene nodes and animation state for one tile, either on the board or in the player's hand.</summary>
public sealed class TileVisual
{
    public required Node3D Root { get; init; }
    public required Node3D Body { get; init; }
    public required MeshInstance3D Ground { get; init; }
    public MeshInstance3D? Props { get; init; }
    public MeshInstance3D? Water { get; init; }
    public required List<AnimatedPart> Parts { get; init; }
    public required TileSpec Tile { get; init; }
    public int Rotation { get; set; }
    public int Slot { get; set; }

    // Physical response: vertical offset, squash and a little wobble.
    public Spring Lift = new(0f, 230f, 15f);
    public Spring Squash = new(0f, 320f, 12f);
    public Spring WobbleX = new(0f, 160f, 7f);
    public Spring WobbleZ = new(0f, 160f, 7f);

    public float Grow = 1f;
    public float GrowSpeed;
    public bool Landed = true;
    public float HighlightAlpha;
    public Color HighlightColor = Colors.White;
    public float PendingImpulse;
    public float PendingImpulseDelay;

    public void SetGrow(float value)
    {
        Grow = value;
        Props?.SetInstanceShaderParameter("grow", value);
        foreach (var part in Parts)
        {
            if (part.Node.GetChildCount() > 0 && part.Node.GetChild(0) is GeometryInstance3D g)
                g.SetInstanceShaderParameter("grow", value);
            part.Node.Visible = value > 0.6f;
        }
    }

    public void SetHighlight(Color color, float alpha)
    {
        HighlightColor = color;
        HighlightAlpha = alpha;
        var value = new Color(color.R, color.G, color.B, alpha);
        Ground.SetInstanceShaderParameter("highlight", value);
        Props?.SetInstanceShaderParameter("highlight", value);
    }

    public void SetDim(float dim)
    {
        Ground.SetInstanceShaderParameter("dim", dim);
        Props?.SetInstanceShaderParameter("dim", dim);
    }

    public void Free()
    {
        if (GodotObject.IsInstanceValid(Root))
            Root.QueueFree();
    }
}
