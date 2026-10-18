using System.Collections.Generic;
using System.Linq;
using Godot;
using LandBuilder.Domain;

namespace LandBuilder.Presentation;

/// <summary>One of the island's biggest regions, as the land panel shows it.</summary>
public sealed record LandEntry(Terrain Terrain, int Size, int Level, string Name, RegionTier? Next, bool Closed);

/// <summary>
/// "Your land": the biggest region of each kind with its current rung and how far it is from the next one, so the
/// payoff for joining tiles is always in view.
/// </summary>
public partial class Hud
{
    private VBoxContainer _landList = null!;
    private Control _landPanel = null!;
    private readonly Dictionary<Terrain, LandRow> _landRows = new();
    private string _landKey = "";

    private void BuildLandPanel()
    {
        var panel = UiKit.Panel(new Color(Palette.Paper, 0.94f), 16, 14, 10, shadow: 6);
        panel.SetAnchorsPreset(LayoutPreset.TopRight);
        panel.GrowHorizontal = GrowDirection.Begin;
        panel.Position = new Vector2(-20, 92);
        panel.CustomMinimumSize = new Vector2(LandRow.Width + 28, 0);
        panel.MouseFilter = MouseFilterEnum.Ignore;
        panel.Visible = false;
        AddChild(panel);
        _landPanel = panel;

        var col = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        col.AddThemeConstantOverride("separation", 6);
        panel.AddChild(col);
        var header = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        col.AddChild(header);
        var title = UiKit.Text("YOUR LAND", 13, Palette.AccentDeep, UiKit.Black);
        title.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        header.AddChild(title);
        header.AddChild(UiKit.Text("join to grow", 12, Palette.InkSoft, UiKit.Bold));
        _landList = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        _landList.AddThemeConstantOverride("separation", 7);
        col.AddChild(_landList);
    }

    public void SetLand(IReadOnlyList<LandEntry> entries)
    {
        _landPanel.Visible = entries.Count > 0;
        var key = string.Join(",", entries.Select(e => e.Terrain));
        if (key != _landKey)
        {
            _landKey = key;
            foreach (var row in _landRows.Values)
                row.Root.QueueFree();
            _landRows.Clear();
            foreach (var entry in entries)
            {
                var row = new LandRow(entry.Terrain);
                _landRows[entry.Terrain] = row;
                _landList.AddChild(row.Root);
            }
        }

        foreach (var entry in entries)
            _landRows[entry.Terrain].Update(entry);
    }

    /// <summary>Flashes the land panel row for a terrain that just levelled up.</summary>
    public void PulseLand(Terrain terrain)
    {
        if (!_landRows.TryGetValue(terrain, out var row))
            return;
        var root = row.Root;
        root.PivotOffset = root.Size / 2f;
        root.Scale = Vector2.One * 1.18f;
        root.Modulate = new Color(1.4f, 1.3f, 1f);
        var tween = root.CreateTween().SetParallel();
        tween.TweenProperty(root, "scale", Vector2.One, 0.5).SetTrans(Tween.TransitionType.Elastic).SetEase(Tween.EaseType.Out);
        tween.TweenProperty(root, "modulate", Colors.White, 0.8);
    }

    private sealed class LandRow
    {
        public const float Width = 250f;
        public readonly Control Root;
        private readonly Label _name;
        private readonly Label _size;
        private readonly Label _next;
        private readonly ProgressBar _bar;
        private readonly HBoxContainer _stars;
        private int _level = -1;

        public LandRow(Terrain terrain)
        {
            var color = Palette.TerrainSwatch(terrain);
            var row = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore, CustomMinimumSize = new Vector2(Width, 0) };
            row.AddThemeConstantOverride("separation", 9);
            Root = row;
            row.AddChild(UiKit.Badge(Icon.ForTerrain(terrain), color, 30));

            var col = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore, SizeFlagsHorizontal = SizeFlags.ExpandFill };
            col.AddThemeConstantOverride("separation", 2);
            row.AddChild(col);
            var top = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
            top.AddThemeConstantOverride("separation", 4);
            col.AddChild(top);
            _name = UiKit.Text("", 15, Palette.Ink, UiKit.Black);
            top.AddChild(_name);
            _stars = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore, SizeFlagsHorizontal = SizeFlags.ExpandFill };
            _stars.AddThemeConstantOverride("separation", 0);
            top.AddChild(_stars);
            _size = UiKit.Text("", 15, color.Darkened(0.3f), UiKit.Black, HorizontalAlignment.Right);
            top.AddChild(_size);
            _bar = UiKit.Bar(color, 6);
            col.AddChild(_bar);
            _next = UiKit.Text("", 12, Palette.InkSoft, UiKit.Bold);
            col.AddChild(_next);
        }

        public void Update(LandEntry entry)
        {
            _name.Text = entry.Name;
            _size.Text = entry.Size.ToString();
            if (entry.Level != _level)
            {
                _level = entry.Level;
                foreach (var child in _stars.GetChildren())
                    child.QueueFree();
                for (var i = 0; i < RegionTiers.MaxLevel; i++)
                    _stars.AddChild(UiKit.Text("★", 11, i < entry.Level ? Palette.Gold.Darkened(0.1f) : new Color(Palette.Ink, 0.15f), UiKit.Black));
            }

            float progress;
            if (entry.Next is { } next)
            {
                var floor = entry.Level == 0 ? 0 : RegionTiers.Get(entry.Terrain, entry.Level).MinSize;
                progress = (entry.Size - floor) / (float)System.Math.Max(1, next.MinSize - floor);
                _next.Text = entry.Closed
                    ? "completed"
                    : $"{next.MinSize - entry.Size} more → {next.Name}  ·  +{next.Points}";
            }
            else
            {
                progress = 1f;
                _next.Text = "top of the ladder!";
            }

            _bar.CreateTween().SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out).TweenProperty(_bar, "value", Mathf.Clamp(progress, 0f, 1f), 0.5);
        }
    }
}
