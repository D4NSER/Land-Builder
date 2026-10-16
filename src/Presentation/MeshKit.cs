using System.Collections.Generic;
using Godot;

namespace LandBuilder.Presentation;

/// <summary>
/// Accumulates flat-shaded triangles with vertex colours and per-vertex custom data, then commits once.
/// CUSTOM0 carries (pivot.xyz, pop-in delay) for the prop shader; COLOR.a carries wind sway weight.
/// </summary>
public sealed class MeshKit
{
    private readonly List<Vector3> _positions = new();
    private readonly List<Vector3> _normals = new();
    private readonly List<Color> _colors = new();
    private readonly List<Color> _custom = new();

    public Transform3D Transform = Transform3D.Identity;
    public Color Custom = new(0, 0, 0, 0);
    public float Sway;

    /// <summary>When set, triangles are flipped so they face away from this local-space point (convex shapes).</summary>
    public Vector3? OrientFrom;

    /// <summary>When positive, positions are snapped to this grid so meshes built separately weld exactly.</summary>
    public float SnapGrid;

    public int VertexCount => _positions.Count;

    /// <summary>
    /// Adds a triangle given counter-clockwise as seen from its front. Godot treats clockwise as front-facing,
    /// so vertices are emitted as a, c, b.
    /// </summary>
    public void Triangle(Vector3 a, Vector3 b, Vector3 c, Color color)
    {
        if (OrientFrom is Vector3 center)
        {
            var local = (b - a).Cross(c - a);
            if (local.Dot(((a + b + c) / 3f) - center) < 0f)
                (b, c) = (c, b);
        }

        a = Transform * a;
        b = Transform * b;
        c = Transform * c;
        var n = (b - a).Cross(c - a);
        if (n.LengthSquared() < 1e-12f)
            return;
        n = n.Normalized();
        Add(a, n, color);
        Add(c, n, color);
        Add(b, n, color);
    }

    public void TriangleSmooth(Vector3 a, Vector3 b, Vector3 c, Vector3 na, Vector3 nb, Vector3 nc, Color ca, Color cb, Color cc)
    {
        var basis = Transform.Basis;
        Add(Transform * a, (basis * na).Normalized(), ca);
        Add(Transform * c, (basis * nc).Normalized(), cc);
        Add(Transform * b, (basis * nb).Normalized(), cb);
    }

    public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Color color)
    {
        Triangle(a, b, c, color);
        Triangle(a, c, d, color);
    }

    private void Add(Vector3 p, Vector3 n, Color color)
    {
        if (SnapGrid > 0f)
            p = (p / SnapGrid).Round() * SnapGrid;
        _positions.Add(p);
        _normals.Add(n);
        _colors.Add(new Color(color.R, color.G, color.B, Sway));
        _custom.Add(Custom);
    }

    // ---------- primitives (all in the current Transform's local space) ----------

    /// <summary>Prism/frustum with <paramref name="sides"/> faces; base centred at origin, extending up Y.</summary>
    public void Frustum(int sides, float bottomRadius, float topRadius, float height, Color side, Color? top = null, Color? bottom = null, float twist = 0f, bool capBottom = false)
    {
        var topColor = top ?? side;
        var previous = OrientFrom;
        OrientFrom = new Vector3(0, height * 0.5f, 0);
        for (var i = 0; i < sides; i++)
        {
            var a0 = twist + (i * Mathf.Tau / sides);
            var a1 = twist + ((i + 1) * Mathf.Tau / sides);
            var b0 = new Vector3(Mathf.Cos(a0) * bottomRadius, 0, Mathf.Sin(a0) * bottomRadius);
            var b1 = new Vector3(Mathf.Cos(a1) * bottomRadius, 0, Mathf.Sin(a1) * bottomRadius);
            var t0 = new Vector3(Mathf.Cos(a0) * topRadius, height, Mathf.Sin(a0) * topRadius);
            var t1 = new Vector3(Mathf.Cos(a1) * topRadius, height, Mathf.Sin(a1) * topRadius);
            if (topRadius > 0.0001f)
            {
                Quad(b0, t0, t1, b1, side);
                Triangle(new Vector3(0, height, 0), t1, t0, topColor);
            }
            else
            {
                Triangle(b0, new Vector3(0, height, 0), b1, side);
            }

            if (capBottom)
                Triangle(Vector3.Zero, b0, b1, bottom ?? side);
        }

        OrientFrom = previous;
    }

    public void Cone(int sides, float radius, float height, Color color, float twist = 0f) =>
        Frustum(sides, radius, 0f, height, color, twist: twist);

    /// <summary>Axis-aligned box sitting on the origin (y from 0 to size.Y).</summary>
    public void Box(Vector3 size, Color color, Color? topColor = null)
    {
        var hx = size.X * 0.5f;
        var hz = size.Z * 0.5f;
        var h = size.Y;
        var previous = OrientFrom;
        OrientFrom = new Vector3(0, h * 0.5f, 0);
        var p000 = new Vector3(-hx, 0, -hz);
        var p100 = new Vector3(hx, 0, -hz);
        var p101 = new Vector3(hx, 0, hz);
        var p001 = new Vector3(-hx, 0, hz);
        var p010 = new Vector3(-hx, h, -hz);
        var p110 = new Vector3(hx, h, -hz);
        var p111 = new Vector3(hx, h, hz);
        var p011 = new Vector3(-hx, h, hz);
        Quad(p001, p011, p111, p101, color); // +z
        Quad(p100, p110, p010, p000, color); // -z
        Quad(p101, p111, p110, p100, color); // +x
        Quad(p000, p010, p011, p001, color); // -x
        Quad(p010, p110, p111, p011, topColor ?? color); // top
        OrientFrom = previous;
    }

    /// <summary>Gabled roof: ridge along X, sitting on y=0, overhanging the given footprint.</summary>
    public void GableRoof(float width, float depth, float height, Color roof, Color gable)
    {
        var hx = width * 0.5f;
        var hz = depth * 0.5f;
        var l0 = new Vector3(-hx, 0, -hz);
        var l1 = new Vector3(-hx, 0, hz);
        var r0 = new Vector3(hx, 0, -hz);
        var r1 = new Vector3(hx, 0, hz);
        var rl = new Vector3(-hx, height, 0);
        var rr = new Vector3(hx, height, 0);
        var previous = OrientFrom;
        OrientFrom = new Vector3(0, height * 0.3f, 0);
        Quad(l1, rl, rr, r1, roof);
        Quad(r0, rr, rl, l0, roof);
        Triangle(l0, rl, l1, gable);
        Triangle(r1, rr, r0, gable);
        Triangle(l0, l1, r1, gable);
        Triangle(l0, r1, r0, gable);
        OrientFrom = previous;
    }

    /// <summary>Low-poly blob (octahedron subdivided once and jittered).</summary>
    public void Blob(Vector3 radii, Color color, int seed, float jitter = 0.12f)
    {
        var verts = new[]
        {
            new Vector3(1, 0, 0), new Vector3(-1, 0, 0), new Vector3(0, 1, 0),
            new Vector3(0, -1, 0), new Vector3(0, 0, 1), new Vector3(0, 0, -1)
        };
        int[][] faces =
        [
            [0, 2, 4], [4, 2, 1], [1, 2, 5], [5, 2, 0],
            [4, 3, 0], [1, 3, 4], [5, 3, 1], [0, 3, 5]
        ];

        Vector3 Shape(Vector3 v, int salt)
        {
            v = v.Normalized();
            var j = 1f + ((Noise.Hash01(seed, salt) - 0.5f) * 2f * jitter);
            return new Vector3(v.X * radii.X, v.Y * radii.Y, v.Z * radii.Z) * j;
        }

        var previous = OrientFrom;
        OrientFrom = Vector3.Zero;

        int Key(Vector3 v) => (Mathf.RoundToInt(v.X * 100) * 7919) + (Mathf.RoundToInt(v.Y * 100) * 104729) + Mathf.RoundToInt(v.Z * 100);

        foreach (var f in faces)
        {
            var a = verts[f[0]];
            var b = verts[f[1]];
            var c = verts[f[2]];
            var ab = (a + b).Normalized();
            var bc = (b + c).Normalized();
            var ca = (c + a).Normalized();
            var sa = Shape(a, Key(a));
            var sb = Shape(b, Key(b));
            var sc = Shape(c, Key(c));
            var sab = Shape(ab, Key(ab));
            var sbc = Shape(bc, Key(bc));
            var sca = Shape(ca, Key(ca));
            // Faces are wound counter-clockwise when seen from outside.
            Triangle(sa, sca, sab, color);
            Triangle(sab, sbc, sb, color);
            Triangle(sca, sc, sbc, color);
            Triangle(sab, sca, sbc, color);
        }

        OrientFrom = previous;
    }

    public ArrayMesh Commit(bool withCustom)
    {
        var st = new SurfaceTool();
        st.Begin(Mesh.PrimitiveType.Triangles);
        if (withCustom)
            st.SetCustomFormat(0, SurfaceTool.CustomFormat.RgbaFloat);
        for (var i = 0; i < _positions.Count; i++)
        {
            st.SetNormal(_normals[i]);
            st.SetColor(_colors[i]);
            if (withCustom)
                st.SetCustom(0, _custom[i]);
            st.AddVertex(_positions[i]);
        }

        return st.Commit();
    }

    public bool IsEmpty => _positions.Count == 0;
}
