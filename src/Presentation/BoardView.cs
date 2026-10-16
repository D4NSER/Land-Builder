using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using LandBuilder.Domain;

namespace LandBuilder.Presentation;

/// <summary>
/// Owns every tile on the island plus the placement affordances (candidate outlines, edge-match markers).
/// Tiles respond physically: they drop and squash on landing, and neighbours ripple from the impact.
/// </summary>
public partial class BoardView : Node3D
{
    private readonly Dictionary<int, TileVisual> _tiles = new();
    private readonly Dictionary<int, MeshInstance3D> _candidates = new();
    private readonly List<MeshInstance3D> _edgeMarkers = new();
    private ShaderMaterial _terrainMaterial = null!;
    private ShaderMaterial _propsMaterial = null!;
    private ShaderMaterial _lakeMaterial = null!;
    private StandardMaterial3D _candidateMaterial = null!;
    private StandardMaterial3D _candidateHintMaterial = null!;
    private StandardMaterial3D _matchMaterial = null!;
    private StandardMaterial3D _mismatchMaterial = null!;
    private ArrayMesh _outlineMesh = null!;
    private ArrayMesh _edgeMarkerMesh = null!;
    private ArrayMesh _shelfMesh = null!;
    private StandardMaterial3D _shelfMaterial = null!;
    private Node3D _candidateRoot = null!;
    private Node3D _markerRoot = null!;
    private float _time;
    private int? _hintSlot;

    public Fx Fx { get; set; } = null!;

    public event Action<int, TileVisual>? TileLanded;

    public IReadOnlyDictionary<int, TileVisual> Tiles => _tiles;

    public override void _Ready()
    {
        _terrainMaterial = new ShaderMaterial { Shader = GD.Load<Shader>("res://shaders/terrain.gdshader") };
        _propsMaterial = new ShaderMaterial { Shader = GD.Load<Shader>("res://shaders/props.gdshader") };
        _lakeMaterial = new ShaderMaterial { Shader = GD.Load<Shader>("res://shaders/water.gdshader") };
        _lakeMaterial.SetShaderParameter("shallow_color", new Color("86d6cc"));
        _lakeMaterial.SetShaderParameter("deep_color", new Color("3a88b4"));
        _lakeMaterial.SetShaderParameter("depth_scale", 0.2f);
        _lakeMaterial.SetShaderParameter("foam_width", 0.02f);
        _lakeMaterial.SetShaderParameter("min_alpha", 0.6f);
        _lakeMaterial.SetShaderParameter("ripple_scale", 6.0f);
        _lakeMaterial.SetShaderParameter("ripple_strength", 0.35f);

        _candidateMaterial = Unshaded(new Color(1, 1, 1, 0.22f));
        _candidateHintMaterial = Unshaded(new Color(Palette.Gold, 0.95f));
        _matchMaterial = Unshaded(new Color("62e887"), onTop: true);
        _mismatchMaterial = Unshaded(new Color("ff5b4d"), onTop: true);

        _outlineMesh = BuildOutlineMesh(0.86f, 0.028f);
        _edgeMarkerMesh = BuildEdgeMarkerMesh();
        _shelfMesh = BuildShelfMesh();
        _shelfMaterial = new StandardMaterial3D { VertexColorUseAsAlbedo = true, Roughness = 1f };
        _candidateRoot = new Node3D { Name = "Candidates" };
        _markerRoot = new Node3D { Name = "EdgeMarkers" };
        AddChild(_candidateRoot);
        AddChild(_markerRoot);

        for (var i = 0; i < 6; i++)
        {
            var marker = new MeshInstance3D { Mesh = _edgeMarkerMesh, Visible = false, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
            _markerRoot.AddChild(marker);
            _edgeMarkers.Add(marker);
        }
    }

    private static StandardMaterial3D Unshaded(Color color, bool onTop = false) => new()
    {
        ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
        Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
        AlbedoColor = color,
        CullMode = BaseMaterial3D.CullModeEnum.Disabled,
        NoDepthTest = onTop,
        RenderPriority = onTop ? 10 : 0
    };

    // ------------------------------------------------------------------ building

    /// <summary>Builds tile nodes. <paramref name="worldCenter"/> anchors noise so the tile blends with its neighbours.</summary>
    public TileVisual CreateTileVisual(TileSpec tile, int rotation, Vector3 worldCenter, int seed)
    {
        var root = new Node3D { Name = "Tile" };
        var body = new Node3D { Name = "Body", Rotation = new Vector3(0, HexLayout.RotationToYaw(rotation), 0) };
        root.AddChild(body);

        var toWorld = new Transform3D(new Basis(Vector3.Up, HexLayout.RotationToYaw(rotation)), worldCenter);
        var sampler = new TerrainSampler(tile, toWorld);
        var ground = new MeshInstance3D
        {
            Name = "Ground",
            Mesh = TileMeshBuilder.BuildGround(sampler),
            MaterialOverride = _terrainMaterial
        };
        body.AddChild(ground);

        MeshInstance3D? water = null;
        var waterMesh = TileMeshBuilder.BuildWater(sampler, tile);
        if (waterMesh is not null)
        {
            water = new MeshInstance3D
            {
                Name = "Water",
                Mesh = waterMesh,
                MaterialOverride = _lakeMaterial,
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off
            };
            body.AddChild(water);
        }

        var partsRoot = new Node3D { Name = "Parts" };
        body.AddChild(partsRoot);
        var builder = new PropBuilder(sampler, tile, seed, partsRoot);
        var propsMesh = builder.Build();
        MeshInstance3D? props = null;
        if (propsMesh is not null)
        {
            props = new MeshInstance3D { Name = "Props", Mesh = propsMesh, MaterialOverride = _propsMaterial };
            body.AddChild(props);
        }

        foreach (var part in builder.Parts)
        {
            foreach (var child in part.Node.GetChildren())
            {
                if (child is MeshInstance3D mi)
                    mi.MaterialOverride = _propsMaterial;
            }
        }

        return new TileVisual
        {
            Root = root,
            Body = body,
            Ground = ground,
            Props = props,
            Water = water,
            Parts = builder.Parts.ToList(),
            Tile = tile,
            Rotation = rotation
        };
    }

    public static int SeedFor(int slot, int turn) => unchecked((slot * 73856093) ^ (turn * 19349663) ^ 0x5bd1e995);

    /// <summary>Adds a tile instantly (loading a save, starter tile) with an optional gentle pop.</summary>
    public TileVisual AddTileImmediate(int slot, PlacedTile placed, bool pop, float delay = 0f)
    {
        RemoveTile(slot);
        var visual = CreateTileVisual(placed.Tile, placed.Rotation, HexLayout.SlotToWorld(slot), SeedFor(slot, placed.Turn));
        visual.Slot = slot;
        visual.Root.Position = HexLayout.SlotToWorld(slot);
        AddChild(visual.Root);
        _tiles[slot] = visual;

        // Sloping sandbank under each tile; overlapping banks form shallows that follow the coastline.
        var jitter = (Noise.Hash01(slot, 7) - 0.5f) * 0.03f;
        visual.Root.AddChild(new MeshInstance3D
        {
            Name = "Shelf",
            Mesh = _shelfMesh,
            MaterialOverride = _shelfMaterial,
            Position = new Vector3(0, jitter, 0),
            Rotation = new Vector3(0, Noise.Hash01(slot, 3) * Mathf.Tau, 0),
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off
        });
        if (pop)
        {
            visual.Landed = false;
            visual.Lift.Snap(2.5f + delay * 4f);
            visual.Lift.Target = 0f;
            visual.SetGrow(0f);
            visual.Root.Visible = true;
        }
        else
        {
            visual.SetGrow(1f);
        }

        return visual;
    }

    /// <summary>Drops a freshly placed tile from <paramref name="fromHeight"/> with a springy landing.</summary>
    public TileVisual DropTile(int slot, PlacedTile placed, float fromHeight, Vector3 tilt)
    {
        var visual = AddTileImmediate(slot, placed, pop: false);
        visual.Landed = false;
        visual.SetGrow(0f);
        visual.Lift.Snap(fromHeight);
        visual.Lift.Target = 0f;
        visual.Lift.Velocity = -2.5f;
        visual.WobbleX.Snap(tilt.X);
        visual.WobbleZ.Snap(tilt.Z);
        visual.WobbleX.Target = 0f;
        visual.WobbleZ.Target = 0f;
        return visual;
    }

    public void RemoveTile(int slot)
    {
        if (_tiles.Remove(slot, out var existing))
            existing.Free();
    }

    /// <summary>Forgets a tile without freeing it so the caller can animate it away.</summary>
    public TileVisual? RemoveTileDeferred(int slot) => _tiles.Remove(slot, out var visual) ? visual : null;

    public void Clear()
    {
        foreach (var tile in _tiles.Values)
            tile.Free();
        _tiles.Clear();
        foreach (var c in _candidates.Values)
            c.QueueFree();
        _candidates.Clear();
        HideEdgeMarkers();
    }

    // ------------------------------------------------------------------ affordances

    public void SetCandidates(IReadOnlyCollection<int> slots, int? hintSlot)
    {
        _hintSlot = hintSlot;
        foreach (var slot in _candidates.Keys.Where(s => !slots.Contains(s)).ToList())
        {
            _candidates[slot].QueueFree();
            _candidates.Remove(slot);
        }

        foreach (var slot in slots)
        {
            if (_candidates.ContainsKey(slot))
                continue;
            var outline = new MeshInstance3D
            {
                Mesh = _outlineMesh,
                MaterialOverride = _candidateMaterial,
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
                Position = HexLayout.SlotToWorld(slot) + new Vector3(0, -0.24f, 0),
                Scale = Vector3.One * 0.01f
            };
            _candidateRoot.AddChild(outline);
            _candidates[slot] = outline;
        }

        foreach (var (slot, outline) in _candidates)
            outline.MaterialOverride = slot == hintSlot ? _candidateHintMaterial : _candidateMaterial;
    }

    public void SetCandidatesVisible(bool visible) => _candidateRoot.Visible = visible;

    /// <summary>Shows green/red markers on each edge of <paramref name="slot"/> that touches a neighbour.</summary>
    public void ShowEdgeMarkers(Vector3 center, PlacementEvaluation eval)
    {
        var index = 0;
        foreach (var edge in eval.MatchedEdges.Concat(eval.MismatchedEdges))
        {
            if (index >= _edgeMarkers.Count)
                break;
            var marker = _edgeMarkers[index++];
            var matched = eval.MatchedEdges.Contains(edge);
            marker.MaterialOverride = matched ? _matchMaterial : _mismatchMaterial;
            marker.Visible = true;
            var dir = HexLayout.EdgeDirection(edge);
            marker.Position = center + (dir * HexLayout.InRadius * 0.97f);
            marker.Rotation = new Vector3(0, HexLayout.EdgeAngle(edge), 0);
            marker.SetMeta("matched", matched);
        }

        for (; index < _edgeMarkers.Count; index++)
            _edgeMarkers[index].Visible = false;
    }

    public void HideEdgeMarkers()
    {
        foreach (var marker in _edgeMarkers)
            marker.Visible = false;
    }

    // ------------------------------------------------------------------ feedback

    /// <summary>Pushes nearby tiles down with a falloff so a landing ripples through the island.</summary>
    public void Ripple(int originSlot, float strength)
    {
        var origin = HexGrid.Decode(originSlot);
        foreach (var (slot, tile) in _tiles)
        {
            if (slot == originSlot)
                continue;
            var distance = HexGrid.Decode(slot).DistanceTo(origin);
            if (distance > 3)
                continue;
            tile.PendingImpulse = -strength / (distance * distance);
            tile.PendingImpulseDelay = (distance - 1) * 0.055f;
        }
    }

    /// <summary>Gold flash + bounce wave over a set of tiles (quest complete, flawless...).</summary>
    public void Celebrate(IEnumerable<int> slots, int originSlot, Color color)
    {
        var origin = HexGrid.Decode(originSlot);
        foreach (var slot in slots.Distinct())
        {
            if (!_tiles.TryGetValue(slot, out var tile))
                continue;
            var distance = HexGrid.Decode(slot).DistanceTo(origin);
            tile.PendingImpulse = 1.6f;
            tile.PendingImpulseDelay = distance * 0.07f;
            tile.SetHighlight(color, 0.9f);
        }
    }

    public void Flash(int slot, Color color, float alpha = 0.8f)
    {
        if (_tiles.TryGetValue(slot, out var tile))
            tile.SetHighlight(color, alpha);
    }

    // ------------------------------------------------------------------ per-frame

    public override void _Process(double delta)
    {
        var dt = (float)delta;
        _time += dt;

        foreach (var (slot, tile) in _tiles)
        {
            if (tile.PendingImpulse != 0f)
            {
                tile.PendingImpulseDelay -= dt;
                if (tile.PendingImpulseDelay <= 0f)
                {
                    tile.Lift.Velocity += tile.PendingImpulse;
                    tile.Squash.Velocity += tile.PendingImpulse * 2.5f;
                    tile.PendingImpulse = 0f;
                }
            }

            var wasAbove = tile.Lift.Value > 0.02f;
            tile.Lift.Step(dt);
            tile.Squash.Step(dt);
            tile.WobbleX.Step(dt);
            tile.WobbleZ.Step(dt);

            if (!tile.Landed && tile.Lift.Value <= 0.02f && wasAbove)
            {
                tile.Landed = true;
                tile.GrowSpeed = 1.15f;
                tile.Squash.Velocity -= Mathf.Clamp(-tile.Lift.Velocity, 0f, 12f) * 0.9f;
                TileLanded?.Invoke(slot, tile);
            }
            else if (!tile.Landed && tile.Lift.Target == 0f && tile.Lift.IsSettled(0.01f))
            {
                tile.Landed = true;
                tile.GrowSpeed = 1.15f;
            }

            if (tile.GrowSpeed > 0f && tile.Grow < 1f)
            {
                tile.SetGrow(Mathf.Min(1f, tile.Grow + (dt * tile.GrowSpeed)));
                if (tile.Grow >= 1f)
                    tile.GrowSpeed = 0f;
            }

            var squash = Mathf.Clamp(tile.Squash.Value, -0.35f, 0.35f);
            tile.Root.Position = HexLayout.SlotToWorld(slot) + new Vector3(0, tile.Lift.Value, 0);
            tile.Root.Scale = new Vector3(1f - (squash * 0.35f), 1f + squash, 1f - (squash * 0.35f));
            tile.Root.Rotation = new Vector3(tile.WobbleX.Value, 0, tile.WobbleZ.Value);

            if (tile.HighlightAlpha > 0.001f)
                tile.SetHighlight(tile.HighlightColor, tile.HighlightAlpha * Mathf.Exp(-dt * 2.2f));

            AnimateParts(tile);
        }

        // Candidate outlines breathe gently; the hint one pulses.
        foreach (var (slot, outline) in _candidates)
        {
            var target = slot == _hintSlot ? 1.0f + (Mathf.Sin(_time * 5f) * 0.05f) : 1f;
            outline.Scale = outline.Scale.Lerp(Vector3.One * target, Ease.Damp(10f, dt));
        }

        var pulse = 0.75f + (0.25f * Mathf.Sin(_time * 6f));
        foreach (var marker in _edgeMarkers)
        {
            if (!marker.Visible)
                continue;
            var matched = marker.HasMeta("matched") && marker.GetMeta("matched").AsBool();
            marker.Scale = matched ? Vector3.One * (0.95f + (pulse * 0.1f)) : new Vector3(1f, 1f, 1f) * (0.9f + (Mathf.Abs(Mathf.Sin(_time * 12f)) * 0.15f));
        }
    }

    private void AnimateParts(TileVisual tile)
    {
        foreach (var part in tile.Parts)
        {
            switch (part.Kind)
            {
                case AnimatedPartKind.WindmillBlades:
                    part.Node.GetChild<Node3D>(0).Rotation = new Vector3(0, 0, (_time * 1.4f) + part.Phase);
                    break;
                case AnimatedPartKind.Boat:
                    var bob = Mathf.Sin((_time * 1.6f) + part.Phase) * 0.006f;
                    var drift = new Vector3(Mathf.Sin((_time * 0.15f) + part.Phase) * 0.06f, bob, Mathf.Cos((_time * 0.11f) + part.Phase) * 0.05f);
                    part.Node.Position = part.BasePosition + drift;
                    part.Node.Rotation = new Vector3(Mathf.Sin((_time * 1.3f) + part.Phase) * 0.05f, part.Node.Rotation.Y + 0.0015f, Mathf.Cos(_time * 1.1f + part.Phase) * 0.04f);
                    break;
            }
        }
    }

    // ------------------------------------------------------------------ meshes

    /// <summary>Thin hexagonal ring used for empty placement spots.</summary>
    private static ArrayMesh BuildOutlineMesh(float radius, float width)
    {
        var kit = new MeshKit();
        var up = Vector3.Up;
        var white = Colors.White;
        for (var i = 0; i < 6; i++)
        {
            var o0 = HexLayout.Corner(i, radius);
            var o1 = HexLayout.Corner((i + 1) % 6, radius);
            var i0 = HexLayout.Corner(i, radius - width);
            var i1 = HexLayout.Corner((i + 1) % 6, radius - width);
            kit.TriangleSmooth(o0, o1, i1, up, up, up, white, white, white);
            kit.TriangleSmooth(o0, i1, i0, up, up, up, white, white, white);
        }

        return kit.Commit(withCustom: false);
    }

    private static ArrayMesh BuildShelfMesh()
    {
        var kit = new MeshKit();
        var up = Vector3.Up;
        const int segments = 24;
        float[] radii = [0f, 1.05f, 1.6f, 2.3f];
        float[] heights = [-0.82f, -0.86f, -1.35f, -2.6f];
        Color[] colors = [new("c9dcae"), new("c9dcae"), new("8fc6b0"), new("3f8fa6")];
        for (var ring = 0; ring < radii.Length - 1; ring++)
        {
            for (var i = 0; i < segments; i++)
            {
                var a0 = i * Mathf.Tau / segments;
                var a1 = (i + 1) * Mathf.Tau / segments;
                var wobble0 = 1f + (Noise.Hash01(i, ring) * 0.12f);
                var wobble1 = 1f + (Noise.Hash01((i + 1) % segments, ring) * 0.12f);
                var wob2 = 1f + (Noise.Hash01(i, ring + 1) * 0.12f);
                var wob3 = 1f + (Noise.Hash01((i + 1) % segments, ring + 1) * 0.12f);
                Vector3 P(int r, float a, float w) => new(Mathf.Cos(a) * radii[r] * w, heights[r], -Mathf.Sin(a) * radii[r] * w);
                var p00 = P(ring, a0, wobble0);
                var p01 = P(ring, a1, wobble1);
                var p10 = P(ring + 1, a0, wob2);
                var p11 = P(ring + 1, a1, wob3);
                kit.TriangleSmooth(p00, p10, p11, up, up, up, colors[ring], colors[ring + 1], colors[ring + 1]);
                if (ring > 0)
                    kit.TriangleSmooth(p00, p11, p01, up, up, up, colors[ring], colors[ring + 1], colors[ring]);
            }
        }

        return kit.Commit(withCustom: false);
    }

    /// <summary>A rounded tab that sits on a tile edge to show whether it matches.</summary>
    private static ArrayMesh BuildEdgeMarkerMesh()
    {
        var kit = new MeshKit();
        var color = Colors.White;
        // Built facing +X (edge 0); rotated per edge at runtime.
        kit.Transform = new Transform3D(Basis.Identity, Vector3.Zero);
        var half = HexLayout.Radius * 0.42f;
        var up = Vector3.Up;
        var a = new Vector3(-0.11f, 0.0f, -half);
        var b = new Vector3(0.0f, 0.0f, -half);
        var c = new Vector3(0.0f, 0.0f, half);
        var d = new Vector3(-0.11f, 0.0f, half);
        kit.TriangleSmooth(a, d, c, up, up, up, color, color, color);
        kit.TriangleSmooth(a, c, b, up, up, up, color, color, color);
        return kit.Commit(withCustom: false);
    }
}
