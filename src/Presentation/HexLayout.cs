using System;
using Godot;
using LandBuilder.Domain;

namespace LandBuilder.Presentation;

/// <summary>
/// Maps axial slots to world space. Tiles are pointy-top hexes; edge <c>e</c> faces the direction at
/// <c>e * 60°</c> measured from +X towards -Z, which matches <see cref="HexGrid"/>'s direction order.
/// </summary>
public static class HexLayout
{
    /// <summary>Corner radius of a tile.</summary>
    public const float Radius = 1.0f;

    /// <summary>Distance from centre to an edge midpoint.</summary>
    public static readonly float InRadius = Radius * Mathf.Sqrt(3f) * 0.5f;

    public static Vector3 SlotToWorld(int slot)
    {
        var c = HexGrid.Decode(slot);
        var x = Radius * Mathf.Sqrt(3f) * (c.Q + (c.R * 0.5f));
        var z = Radius * 1.5f * c.R;
        return new Vector3(x, 0f, z);
    }

    public static int WorldToSlot(Vector3 world)
    {
        var q = ((Mathf.Sqrt(3f) / 3f * world.X) - (world.Z / 3f)) / Radius;
        var r = (2f / 3f * world.Z) / Radius;
        return HexGrid.Encode(AxialRound(q, r));
    }

    private static HexCoord AxialRound(float q, float r)
    {
        var s = -q - r;
        var rq = Mathf.Round(q);
        var rr = Mathf.Round(r);
        var rs = Mathf.Round(s);
        var dq = Mathf.Abs(rq - q);
        var dr = Mathf.Abs(rr - r);
        var ds = Mathf.Abs(rs - s);
        if (dq > dr && dq > ds)
            rq = -rr - rs;
        else if (dr > ds)
            rr = -rq - rs;
        return new HexCoord((int)rq, (int)rr);
    }

    /// <summary>Angle (radians) of an edge/sector direction, measured from +X towards -Z.</summary>
    public static float EdgeAngle(int edge) => edge * Mathf.Pi / 3f;

    /// <summary>Unit vector pointing out of edge <paramref name="edge"/> in the XZ plane.</summary>
    public static Vector3 EdgeDirection(int edge) => AngleToDirection(EdgeAngle(edge));

    public static Vector3 AngleToDirection(float angle) => new(Mathf.Cos(angle), 0f, -Mathf.Sin(angle));

    /// <summary>Corner <paramref name="i"/> sits between edges i and i+1 (at 30° + i*60°).</summary>
    public static Vector3 Corner(int i, float radius = Radius) => AngleToDirection((Mathf.Pi / 6f) + (i * Mathf.Pi / 3f)) * radius;

    /// <summary>Y rotation (radians) that applies a tile rotation of <paramref name="rotation"/> sixth-turns.</summary>
    public static float RotationToYaw(int rotation) => rotation * Mathf.Pi / 3f;

    /// <summary>Angle of a local XZ point in [0, 2π), using the same convention as <see cref="EdgeAngle"/>.</summary>
    public static float PointAngle(float x, float z)
    {
        var a = Mathf.Atan2(-z, x);
        return a < 0 ? a + Mathf.Tau : a;
    }

    /// <summary>Distance from a local point to the hex outline (positive inside).</summary>
    public static float DistanceToHexEdge(float x, float z)
    {
        var best = float.MaxValue;
        for (var e = 0; e < 6; e++)
        {
            var a = EdgeAngle(e);
            var along = (x * Mathf.Cos(a)) - (z * Mathf.Sin(a));
            best = Math.Min(best, InRadius - along);
        }

        return best;
    }
}
