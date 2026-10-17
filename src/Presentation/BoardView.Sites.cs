using System.Collections.Generic;
using System.Linq;
using Godot;
using LandBuilder.Domain;

namespace LandBuilder.Presentation;

/// <summary>
/// Wonder sites on the board. A blueprint shows golden outlines over its plots, a pale fill on plots still waiting
/// for a tile and scaffolding on the ones that have it; once every plot is filled, the landmark rises over the whole
/// footprint with its surroundings reaching out over each plot.
/// </summary>
public partial class BoardView
{
    private sealed class SiteVisual
    {
        public required WonderSite Site { get; set; }
        public required Node3D Root { get; init; }
        public string Key = "";
        public readonly List<GeometryInstance3D> Growing = new();
        public readonly List<AnimatedPart> Parts = new();
        public float Grow = 1f;
        public float GrowSpeed;
        public float GrowDelay;
    }

    private readonly Dictionary<int, SiteVisual> _sites = new();

    public int SiteCount => _sites.Count;
    private StandardMaterial3D? _plotLineMaterial;
    private StandardMaterial3D? _plotFillMaterial;
    private Node3D? _ghost;
    private string _ghostKey = "";

    private StandardMaterial3D PlotLineMaterial => _plotLineMaterial ??= Unshaded(new Color(Palette.Gold, 0.95f));

    private StandardMaterial3D PlotFillMaterial => _plotFillMaterial ??= new StandardMaterial3D
    {
        ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
        Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
        VertexColorUseAsAlbedo = true,
        CullMode = BaseMaterial3D.CullModeEnum.Disabled
    };

    /// <summary>Ground height at a world point, from whichever tile is there (or just below sea level if none).</summary>
    public float WorldHeight(float x, float z)
    {
        var slot = HexLayout.WorldToSlot(new Vector3(x, 0, z));
        if (!_tiles.TryGetValue(slot, out var visual) || visual.Sampler is not TerrainSampler sampler)
            return TerrainSampler.WaterLevel - 0.08f;
        var local = sampler.ToLocalTransform * new Vector3(x, 0, z);
        return sampler.Evaluate(local.X, local.Z).Height;
    }

    /// <summary>Brings site visuals in line with the rules: new blueprints, freshly filled plots, finished wonders.</summary>
    public void SyncSites(GameState state, bool animate)
    {
        var ids = state.Sites.Select(s => s.Id).ToHashSet();
        foreach (var id in _sites.Keys.Where(id => !ids.Contains(id)).ToList())
        {
            var gone = _sites[id];
            _sites.Remove(id);
            ShrinkAway(gone.Root);
        }

        foreach (var site in state.Sites)
        {
            var progress = Wonders.Progress(state, site);
            var key = site.Built ? "built" : string.Join("", progress.Plots.Select(p => p.Filled ? '1' : '0'));
            if (!_sites.TryGetValue(site.Id, out var visual))
            {
                visual = new SiteVisual { Site = site, Root = new Node3D { Name = $"Site{site.Id}" } };
                AddChild(visual.Root);
                _sites[site.Id] = visual;
            }

            if (visual.Key == key)
                continue;
            var wasBuilt = visual.Key == "built";
            var fresh = visual.Key == "";
            visual.Key = key;
            visual.Site = site;
            foreach (var child in visual.Root.GetChildren())
                child.QueueFree();
            visual.Growing.Clear();
            visual.Parts.Clear();

            if (site.Built)
            {
                LevelLot(state, site);
                BuildFinished(visual);
                StartGrow(visual, animate && !wasBuilt ? 0.3f : 0f, animate && !wasBuilt ? 0.3f : 0f);
            }
            else
            {
                BuildBlueprint(visual, progress);
                StartGrow(visual, animate ? 1.4f : 0f, animate && !fresh ? 0.55f : 0f);
            }
        }
    }

    private void ClearSites()
    {
        foreach (var visual in _sites.Values)
            visual.Root.QueueFree();
        _sites.Clear();
        HideBlueprint();
    }

    private static void ShrinkAway(Node3D node)
    {
        var tween = node.CreateTween();
        tween.TweenProperty(node, "scale", Vector3.One * 0.01f, 0.35).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.In);
        tween.TweenCallback(Callable.From(node.QueueFree));
    }

    private static void StartGrow(SiteVisual visual, float speed, float delay)
    {
        visual.Grow = speed > 0f ? 0f : 1f;
        visual.GrowSpeed = speed;
        visual.GrowDelay = delay;
        foreach (var g in visual.Growing)
            g.SetInstanceShaderParameter("grow", visual.Grow);
        foreach (var part in visual.Parts)
            part.Node.Visible = visual.Grow > 0.85f;
    }

    /// <summary>Rebuilds the ground under a just-finished wonder at once, so the landmark is placed on its lot.</summary>
    private void LevelLot(GameState state, WonderSite site)
    {
        if (ContextProvider is null)
            return;
        var near = Wonders.Footprint(site).SelectMany(s => HexGrid.GetNeighborSlots(s).Append(s)).Distinct();
        foreach (var slot in near)
        {
            if (!_tiles.TryGetValue(slot, out var tile) || !state.Board.ContainsKey(slot))
                continue;
            var context = ContextProvider(slot, tile.Rotation);
            if (context.TerrainKey != tile.TerrainKey)
                RebuildTerrain(tile, context);
        }
    }

    private void BuildFinished(SiteVisual visual)
    {
        var site = visual.Site;
        var centre = WonderLayout.Centroid(site);
        var scale = WonderLayout.Scale(site.Type);
        var lot = WonderLayout.LotHeight(site.Type);
        var yaw = WonderLayout.Yaw(site);
        var seed = SeedFor(site.Anchor, site.Id);

        // The landmark, authored at tile size and scaled up around its lot.
        var holderY = lot - (scale * TerrainSampler.LotHeight);
        var holder = new Node3D
        {
            Name = "Landmark",
            Position = new Vector3(centre.X, holderY, centre.Z),
            Rotation = new Vector3(0, yaw, 0),
            Scale = Vector3.One * scale
        };
        visual.Root.AddChild(holder);
        var basis = new Basis(Vector3.Up, yaw);
        float LocalGround(float x, float z)
        {
            var world = centre + (basis * new Vector3(x * scale, 0, z * scale));
            return (WorldHeight(world.X, world.Z) - holderY) / scale;
        }

        var builder = new WonderModelBuilder(LocalGround, Style, holder, seed, (TerrainSampler.WaterLevel - holderY) / scale);
        AddGrowing(visual, holder, builder.Build(site.Type), builder.Parts);

        // Walls, piers, gardens... over each plot, in an unscaled frame.
        var frame = new Node3D { Name = "Surroundings", Position = centre with { Y = 0 } };
        visual.Root.AddChild(frame);
        var around = new WonderModelBuilder((x, z) => WorldHeight(centre.X + x, centre.Z + z), Style, frame, seed + 1);
        AddGrowing(visual, frame, around.BuildSurroundings(site.Type, WonderLayout.Plots(site)), around.Parts);
    }

    private void BuildBlueprint(SiteVisual visual, WonderProgress progress)
    {
        var seed = SeedFor(visual.Site.Anchor, visual.Site.Id);
        foreach (var plot in progress.Plots)
        {
            var centre = HexLayout.SlotToWorld(plot.Slot);
            visual.Root.AddChild(PlotOutline(centre, plot.Filled, plot.Filled ? Palette.Gold : new Color("ffe8a6")));
            if (!plot.Filled)
            {
                visual.Root.AddChild(PlotFill(centre, new Color(1f, 0.93f, 0.7f, 0.22f)));
                continue;
            }

            var holder = new Node3D { Name = "Scaffold", Position = centre with { Y = 0 }, Scale = Vector3.One * 0.8f };
            visual.Root.AddChild(holder);
            var builder = new WonderModelBuilder((x, z) => WorldHeight(centre.X + (x * 0.8f), centre.Z + (z * 0.8f)) / 0.8f, Style, holder, seed + plot.Slot);
            AddGrowing(visual, holder, builder.BuildScaffold(visual.Site.Type), builder.Parts);
        }
    }

    private void AddGrowing(SiteVisual visual, Node3D parent, ArrayMesh mesh, IReadOnlyList<AnimatedPart> parts)
    {
        var instance = new MeshInstance3D { Mesh = mesh, MaterialOverride = _propsMaterial };
        parent.AddChild(instance);
        visual.Growing.Add(instance);
        PrepareParts(parts);
        visual.Parts.AddRange(parts);
        foreach (var part in parts)
        {
            if (part.Node.GetChildCount() > 0 && part.Node.GetChild(0) is GeometryInstance3D g)
                visual.Growing.Add(g);
        }
    }

    /// <summary>A golden hex ring hugging the ground of a plot (or floating just above the sea on an empty one).</summary>
    private MeshInstance3D PlotOutline(Vector3 centre, bool onGround, Color color)
    {
        var kit = new MeshKit();
        const int steps = 5;
        var up = Vector3.Up;
        for (var i = 0; i < 6; i++)
        {
            for (var s = 0; s < steps; s++)
            {
                Vector3 Point(int corner, float t, float radius)
                {
                    var a = HexLayout.Corner(corner, radius);
                    var b = HexLayout.Corner((corner + 1) % 6, radius);
                    var p = centre + a.Lerp(b, t);
                    var y = onGround ? Mathf.Max(WorldHeight(p.X, p.Z), TerrainSampler.WaterLevel) + 0.03f : 0.06f;
                    return p with { Y = y };
                }

                var t0 = s / (float)steps;
                var t1 = (s + 1) / (float)steps;
                var o0 = Point(i, t0, 0.93f);
                var o1 = Point(i, t1, 0.93f);
                var i0 = Point(i, t0, 0.86f);
                var i1 = Point(i, t1, 0.86f);
                kit.TriangleSmooth(o0, o1, i1, up, up, up, color, color, color);
                kit.TriangleSmooth(o0, i1, i0, up, up, up, color, color, color);
            }
        }

        return new MeshInstance3D { Mesh = kit.Commit(withCustom: false), MaterialOverride = PlotLineMaterial, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
    }

    private MeshInstance3D PlotFill(Vector3 centre, Color color)
    {
        var kit = new MeshKit();
        var up = Vector3.Up;
        var c = centre + new Vector3(0, 0.05f, 0);
        for (var i = 0; i < 6; i++)
        {
            var a = c + HexLayout.Corner(i, 0.86f);
            var b = c + HexLayout.Corner((i + 1) % 6, 0.86f);
            kit.TriangleSmooth(c, a, b, up, up, up, color, color, color);
        }

        return new MeshInstance3D { Mesh = kit.Commit(withCustom: false), MaterialOverride = PlotFillMaterial, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
    }

    // ------------------------------------------------------------------ placement ghost

    /// <summary>
    /// Previews a blueprint while a wonder card is held: green plots already have a fitting tile, gold ones still
    /// need one, red ones block the spot.
    /// </summary>
    public void ShowBlueprint(SitePlan? plan)
    {
        if (plan is null || plan.Plots.Count == 0)
        {
            HideBlueprint();
            return;
        }

        var key = $"{plan.Type}|{plan.Anchor}|{plan.Rotation}|{plan.IsValid}";
        if (key == _ghostKey && _ghost is not null)
            return;
        HideBlueprint();
        _ghostKey = key;
        _ghost = new Node3D { Name = "Blueprint" };
        AddChild(_ghost);
        var claimed = _sites.Values.SelectMany(v => Wonders.Footprint(v.Site)).ToHashSet();
        foreach (var plot in plan.Plots)
        {
            var centre = HexLayout.SlotToWorld(plot.Slot);
            var blocked = claimed.Contains(plot.Slot) || (plot.Filled && !plot.Suits);
            var color = blocked ? new Color("ff6a55") : plot.Filled ? new Color("62e887") : new Color("ffd166");
            _ghost.AddChild(PlotOutline(centre, plot.Filled, color));
            if (!plot.Filled || blocked)
                _ghost.AddChild(PlotFill(centre with { Y = plot.Filled ? 0.12f : 0f }, new Color(color, 0.3f)));
        }
    }

    public void HideBlueprint()
    {
        _ghost?.QueueFree();
        _ghost = null;
        _ghostKey = "";
    }

    /// <summary>A small model of the finished wonder for the hand while its card is held.</summary>
    public TileVisual CreateWonderHand(WonderType type)
    {
        var root = new Node3D { Name = "WonderHand" };
        var body = new Node3D { Name = "Body", Scale = Vector3.One * 0.55f };
        root.AddChild(body);
        var ground = new MeshInstance3D { Name = "Ground" };
        body.AddChild(ground);
        var builder = new WonderModelBuilder((_, _) => TerrainSampler.LotHeight, Style, body, 4242, TerrainSampler.LotHeight - 0.08f);
        var props = new MeshInstance3D { Name = "Props", Mesh = builder.Build(type), MaterialOverride = _propsMaterial };
        body.AddChild(props);
        PrepareParts(builder.Parts);
        return new TileVisual
        {
            Root = root,
            Body = body,
            Ground = ground,
            Props = props,
            Parts = builder.Parts.ToList(),
            Tile = TileSpec.Pure(Terrain.Grass)
        };
    }

    private void ProcessSites(float dt)
    {
        foreach (var visual in _sites.Values)
        {
            if (visual.GrowSpeed > 0f)
            {
                if (visual.GrowDelay > 0f)
                {
                    visual.GrowDelay -= dt;
                }
                else
                {
                    visual.Grow = Mathf.Min(1f, visual.Grow + (dt * visual.GrowSpeed));
                    foreach (var g in visual.Growing)
                        g.SetInstanceShaderParameter("grow", visual.Grow);
                    foreach (var part in visual.Parts)
                        part.Node.Visible = visual.Grow > 0.85f;
                    if (visual.Grow >= 1f)
                        visual.GrowSpeed = 0f;
                }
            }

            AnimateParts(visual.Parts);
        }

        if (_plotLineMaterial is not null)
            _plotLineMaterial.AlbedoColor = new Color(Palette.Gold, 0.7f + (0.3f * Mathf.Sin(_time * 3f)));
    }

    /// <summary>World position above a site, for HUD pins.</summary>
    public static Vector3 SitePinPosition(WonderSite site) => WonderLayout.Centroid(site) + new Vector3(0, 1.1f, 0);
}
