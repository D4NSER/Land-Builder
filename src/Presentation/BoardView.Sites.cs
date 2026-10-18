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

        /// <summary>Tiles whose ground must be levelled before the landmark can rise (one per frame).</summary>
        public Queue<int>? LotQueue;

        /// <summary>The translucent floor of each plot still waiting for a tile.</summary>
        public readonly Dictionary<int, MeshInstance3D> Fills = new();
    }

    private readonly Dictionary<int, SiteVisual> _sites = new();

    public int SiteCount => _sites.Count;
    private StandardMaterial3D? _plotLineMaterial;
    private StandardMaterial3D? _plotFillMaterial;
    private Node3D? _ghost;
    private string _ghostKey = "";

    private StandardMaterial3D? _fitMaterial;
    private StandardMaterial3D? _misfitMaterial;
    private IReadOnlyDictionary<int, bool> _plotFits = new Dictionary<int, bool>();

    /// <summary>Rings and stakes: opaque and vertex-coloured, so they read clearly over land and sea alike.</summary>
    private StandardMaterial3D PlotLineMaterial => _plotLineMaterial ??= new StandardMaterial3D
    {
        ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
        VertexColorUseAsAlbedo = true,
        CullMode = BaseMaterial3D.CullModeEnum.Disabled
    };

    private static StandardMaterial3D FillMaterial(Color color) => new()
    {
        ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
        Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
        AlbedoColor = color,
        CullMode = BaseMaterial3D.CullModeEnum.Disabled,
        RenderPriority = 4
    };

    private StandardMaterial3D PlotFillMaterial => _plotFillMaterial ??= FillMaterial(new Color(1f, 0.9f, 0.55f, 0.28f));
    private StandardMaterial3D FitMaterial => _fitMaterial ??= FillMaterial(new Color(0.38f, 0.92f, 0.52f, 0.45f));
    private StandardMaterial3D MisfitMaterial => _misfitMaterial ??= FillMaterial(new Color(0.6f, 0.6f, 0.6f, 0.12f));

    /// <summary>
    /// Tints every waiting plot by whether the tile in hand suits it: green where it would fit, faded where it
    /// would not. Pass an empty map to clear.
    /// </summary>
    public void SetPlotFit(IReadOnlyDictionary<int, bool> fits)
    {
        _plotFits = fits;
        foreach (var visual in _sites.Values)
        {
            foreach (var (slot, fill) in visual.Fills)
                fill.MaterialOverride = FillFor(slot);
        }
    }

    private StandardMaterial3D FillFor(int slot) =>
        _plotFits.TryGetValue(slot, out var fits) ? (fits ? FitMaterial : MisfitMaterial) : PlotFillMaterial;

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
            visual.Fills.Clear();

            if (site.Built)
            {
                if (animate && !wasBuilt)
                {
                    // Level the lot over the next few frames, then raise the landmark (see ProcessSites).
                    visual.LotQueue = new Queue<int>(SlotsToLevel(state, site));
                }
                else
                {
                    foreach (var slot in SlotsToLevel(state, site))
                        LevelTile(slot);
                    BuildFinished(visual);
                    StartGrow(visual, 0f, 0f);
                }
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

    /// <summary>Tiles under and around a finished wonder whose ground has to change for its lot.</summary>
    private List<int> SlotsToLevel(GameState state, WonderSite site)
    {
        var result = new List<int>();
        if (ContextProvider is null)
            return result;
        foreach (var slot in Wonders.Footprint(site).SelectMany(s => HexGrid.GetNeighborSlots(s).Append(s)).Distinct())
        {
            if (_tiles.TryGetValue(slot, out var tile) && state.Board.ContainsKey(slot) && ContextProvider(slot, tile.Rotation).TerrainKey != tile.TerrainKey)
                result.Add(slot);
        }

        return result;
    }

    private void LevelTile(int slot)
    {
        if (ContextProvider is null || !_tiles.TryGetValue(slot, out var tile))
            return;
        var context = ContextProvider(slot, tile.Rotation);
        if (context.TerrainKey != tile.TerrainKey)
            RebuildTerrain(tile, context);
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
            visual.Root.AddChild(PlotOutline(centre, plot.Filled, Palette.Gold));
            if (!plot.Filled)
            {
                var fill = PlotFill(centre, FillFor(plot.Slot));
                visual.Fills[plot.Slot] = fill;
                visual.Root.AddChild(fill);
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

    /// <summary>
    /// A hex ring hugging the ground of a plot. Empty plots float theirs just above the sea on six survey stakes
    /// with pennants, so a footprint reaching off the island is plain to see.
    /// </summary>
    private MeshInstance3D PlotOutline(Vector3 centre, bool onGround, Color color)
    {
        var kit = new MeshKit();
        if (!onGround)
        {
            var post = new Color("8b6a4a");
            for (var i = 0; i < 6; i++)
            {
                var p = centre + HexLayout.Corner(i, 0.9f);
                kit.Transform = new Transform3D(Basis.Identity, p with { Y = -0.35f });
                kit.Box(new Vector3(0.03f, 0.58f, 0.03f), post);
                kit.Transform = new Transform3D(new Basis(Vector3.Up, i * Mathf.Pi / 3f), p with { Y = 0.17f });
                kit.Quad(new Vector3(0, 0, 0), new Vector3(0, 0.07f, 0), new Vector3(0.09f, 0.035f, 0), new Vector3(0.09f, 0.035f, 0), color);
                kit.Quad(new Vector3(0.09f, 0.035f, 0), new Vector3(0.09f, 0.035f, 0), new Vector3(0, 0.07f, 0), new Vector3(0, 0, 0), color);
            }

            kit.Transform = Transform3D.Identity;
        }

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
                    var y = onGround ? Mathf.Max(WorldHeight(p.X, p.Z), TerrainSampler.WaterLevel) + 0.03f : 0.1f;
                    return p with { Y = y };
                }

                var t0 = s / (float)steps;
                var t1 = (s + 1) / (float)steps;
                var o0 = Point(i, t0, 0.94f);
                var o1 = Point(i, t1, 0.94f);
                var i0 = Point(i, t0, 0.85f);
                var i1 = Point(i, t1, 0.85f);
                kit.TriangleSmooth(o0, o1, i1, up, up, up, color, color, color);
                kit.TriangleSmooth(o0, i1, i0, up, up, up, color, color, color);
            }
        }

        return new MeshInstance3D { Mesh = kit.Commit(withCustom: false), MaterialOverride = PlotLineMaterial, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
    }

    private MeshInstance3D PlotFill(Vector3 centre, Material material)
    {
        var kit = new MeshKit();
        var up = Vector3.Up;
        var color = Colors.White;
        var c = centre + new Vector3(0, 0.08f, 0);
        for (var i = 0; i < 6; i++)
        {
            var a = c + HexLayout.Corner(i, 0.86f);
            var b = c + HexLayout.Corner((i + 1) % 6, 0.86f);
            kit.TriangleSmooth(c, a, b, up, up, up, color, color, color);
        }

        return new MeshInstance3D { Mesh = kit.Commit(withCustom: false), MaterialOverride = material, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
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
                _ghost.AddChild(PlotFill(centre with { Y = plot.Filled ? 0.12f : 0f }, FillMaterial(new Color(color, 0.35f))));
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
            if (visual.LotQueue is { } lot)
            {
                if (lot.Count > 0 && FrameBudget.TryClaim())
                    LevelTile(lot.Dequeue());
                if (lot.Count == 0)
                {
                    visual.LotQueue = null;
                    BuildFinished(visual);
                    StartGrow(visual, 0.3f, 0.1f);
                }

                continue;
            }

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

        if (_fitMaterial is not null)
            _fitMaterial.AlbedoColor = new Color(0.38f, 0.92f, 0.52f, 0.32f + (0.2f * Mathf.Sin(_time * 4f)));
    }

    /// <summary>World position above a site, for HUD pins.</summary>
    public static Vector3 SitePinPosition(WonderSite site) => WonderLayout.Centroid(site) + new Vector3(0, 1.1f, 0);
}
