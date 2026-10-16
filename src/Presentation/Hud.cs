using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using LandBuilder.Domain;

namespace LandBuilder.Presentation;

/// <summary>
/// In-game overlay: score with animated counter and milestone bar, streak, quest cards, the tile stack with live 3D
/// previews, floating score popups, banners and toasts. Built in code so layout stays in one readable place.
/// </summary>
public partial class Hud : Control
{
    private readonly Audio _audio;
    private readonly Func<TileSpec, int, TileVisual> _tileFactory;
    private readonly Dictionary<int, QuestCard> _questCards = new();
    private readonly Dictionary<int, Control> _questPins = new();

    private Label _scoreLabel = null!;
    private Control _scoreBox = null!;
    private Label _bestLabel = null!;
    private ProgressBar _milestoneBar = null!;
    private Label _milestoneLabel = null!;
    private PanelContainer _streakPill = null!;
    private Label _streakLabel = null!;
    private VBoxContainer _questList = null!;
    private Label _questHeader = null!;
    private TilePreview _handPreview = null!;
    private TilePreview _nextPreview = null!;
    private Label _tilesLeftLabel = null!;
    private Control _stackCard = null!;
    private Control _stackShadows = null!;
    private Button _undoButton = null!;
    private Button _hintButton = null!;
    private Label _bannerTitle = null!;
    private Label _bannerSubtitle = null!;
    private Control _banner = null!;
    private PanelContainer _toast = null!;
    private Label _toastLabel = null!;
    private Control _popupLayer = null!;
    private Control _pinLayer = null!;
    private Label _controlsHint = null!;
    private Control _hintPanel = null!;
    private Label _hintLabel = null!;
    private PanelContainer _hoverInfo = null!;
    private Label _hoverInfoLabel = null!;

    private double _displayScore;
    private long _targetScore;
    private float _scorePulse;
    private float _toastTimer;
    private int _lastTilesLeft = -1;
    private Tween? _bannerTween;
    private float _milestoneDisplay;
    private float _milestoneTarget;

    public event Action? UndoPressed;
    public event Action? HintPressed;
    public event Action? PausePressed;

    public Hud(Audio audio, Func<TileSpec, int, TileVisual> tileFactory)
    {
        _audio = audio;
        _tileFactory = tileFactory;
        SetAnchorsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Ignore;
    }

    public Vector2 ScoreAnchor => _scoreBox.GlobalPosition + (_scoreBox.Size / 2f);
    public Vector2 StackAnchor => _stackCard.GlobalPosition + (_stackCard.Size / 2f);

    public override void _Ready()
    {
        _pinLayer = new Control { MouseFilter = MouseFilterEnum.Ignore };
        _pinLayer.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(_pinLayer);

        BuildTopBar();
        BuildQuestPanel();
        BuildStack();
        BuildBanner();
        BuildToast();
        BuildHintPanel();

        _hoverInfo = new PanelContainer { MouseFilter = MouseFilterEnum.Ignore, Visible = false };
        _hoverInfo.AddThemeStyleboxOverride("panel", UiKit.Box(new Color(Palette.Ink, 0.82f), 12, padH: 10, padV: 3));
        _hoverInfoLabel = UiKit.Text("", 17, Colors.White, UiKit.Black, HorizontalAlignment.Center);
        _hoverInfo.AddChild(_hoverInfoLabel);
        AddChild(_hoverInfo);

        _popupLayer = new Control { MouseFilter = MouseFilterEnum.Ignore };
        _popupLayer.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(_popupLayer);
    }

    /// <summary>Small "+30 · Perfect" preview that follows the floating tile.</summary>
    public void SetHoverInfo(Vector2? screen, string text, Color color)
    {
        if (screen is not Vector2 s || string.IsNullOrEmpty(text))
        {
            _hoverInfo.Visible = false;
            return;
        }

        _hoverInfo.Visible = true;
        _hoverInfoLabel.Text = text;
        _hoverInfoLabel.LabelSettings.FontColor = color;
        _hoverInfo.ResetSize();
        _hoverInfo.Position = s - new Vector2(_hoverInfo.Size.X / 2f, _hoverInfo.Size.Y + 6);
    }

    // ------------------------------------------------------------------ layout

    private void BuildTopBar()
    {
        // Score, centred.
        var scorePanel = UiKit.Panel(padH: 30, padV: 10);
        scorePanel.SetAnchorsPreset(LayoutPreset.CenterTop);
        scorePanel.GrowHorizontal = GrowDirection.Both;
        scorePanel.Position = new Vector2(0, 18);
        scorePanel.CustomMinimumSize = new Vector2(380, 0);
        AddChild(scorePanel);

        var col = new VBoxContainer { Alignment = BoxContainer.AlignmentMode.Center, MouseFilter = MouseFilterEnum.Ignore };
        col.AddThemeConstantOverride("separation", 2);
        scorePanel.AddChild(col);

        var scoreRow = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center, MouseFilter = MouseFilterEnum.Ignore };
        scoreRow.AddThemeConstantOverride("separation", 14);
        col.AddChild(scoreRow);

        _scoreBox = new CenterContainer { MouseFilter = MouseFilterEnum.Ignore };
        _scoreLabel = UiKit.Text("0", 46, Palette.Ink, UiKit.Black, HorizontalAlignment.Center);
        _scoreBox.AddChild(_scoreLabel);
        scoreRow.AddChild(_scoreBox);

        _streakPill = new PanelContainer { MouseFilter = MouseFilterEnum.Ignore, Visible = false };
        _streakPill.AddThemeStyleboxOverride("panel", UiKit.Box(Palette.Accent, 14, padH: 12, padV: 2));
        _streakLabel = UiKit.Text("x1.0", 20, Colors.White, UiKit.Black, HorizontalAlignment.Center);
        _streakPill.AddChild(_streakLabel);
        scoreRow.AddChild(_streakPill);

        _milestoneBar = UiKit.Bar(Palette.Accent, 10);
        _milestoneBar.CustomMinimumSize = new Vector2(320, 10);
        col.AddChild(_milestoneBar);
        _milestoneLabel = UiKit.Text("", 15, Palette.InkSoft, UiKit.Bold, HorizontalAlignment.Center);
        col.AddChild(_milestoneLabel);

        // Best score, top-left.
        var left = UiKit.Panel(padH: 18, padV: 8);
        left.Position = new Vector2(20, 20);
        AddChild(left);
        var leftCol = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        leftCol.AddThemeConstantOverride("separation", -2);
        left.AddChild(leftCol);
        leftCol.AddChild(UiKit.Text("LAND BUILDER", 15, Palette.AccentDeep, UiKit.Black));
        _bestLabel = UiKit.Text("Best 0", 17, Palette.InkSoft, UiKit.Bold);
        leftCol.AddChild(_bestLabel);

        // Action buttons, top-right.
        var buttons = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        buttons.AddThemeConstantOverride("separation", 10);
        buttons.SetAnchorsPreset(LayoutPreset.TopRight);
        buttons.GrowHorizontal = GrowDirection.Begin;
        buttons.Position = new Vector2(-20, 20);
        AddChild(buttons);
        _hintButton = UiKit.IconButton("?", "Hint (H): show the best spot", () => HintPressed?.Invoke(), _audio);
        _hintButton.Text = "💡";
        _undoButton = UiKit.IconButton("↶", "Undo (Ctrl+Z)", () => UndoPressed?.Invoke(), _audio, 54);
        _undoButton.CustomMinimumSize = new Vector2(78, 54);
        var pause = UiKit.IconButton("❚❚", "Pause (Esc)", () => PausePressed?.Invoke(), _audio);
        pause.AddThemeFontSizeOverride("font_size", 18);
        buttons.AddChild(_hintButton);
        buttons.AddChild(_undoButton);
        buttons.AddChild(pause);
    }

    private void BuildQuestPanel()
    {
        var wrapper = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        wrapper.Position = new Vector2(20, 104);
        wrapper.AddThemeConstantOverride("separation", 8);
        AddChild(wrapper);
        _questHeader = UiKit.Text("QUESTS", 14, new Color(Palette.Paper, 0.95f), UiKit.Black, outline: 6, outlineColor: new Color(Palette.Ink, 0.55f));
        wrapper.AddChild(_questHeader);
        _questList = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        _questList.AddThemeConstantOverride("separation", 8);
        wrapper.AddChild(_questList);
    }

    private void BuildStack()
    {
        var anchor = new Control { MouseFilter = MouseFilterEnum.Ignore };
        anchor.SetAnchorsPreset(LayoutPreset.BottomLeft);
        anchor.Position = new Vector2(24, -24);
        AddChild(anchor);

        // Card "shadows" suggest a physical stack; how many show depends on tiles left.
        _stackShadows = new Control { MouseFilter = MouseFilterEnum.Ignore };
        anchor.AddChild(_stackShadows);
        for (var i = 3; i >= 1; i--)
        {
            var shadow = new Panel { MouseFilter = MouseFilterEnum.Ignore, Size = new Vector2(200, 200), Position = new Vector2(i * 5, -200 + (i * 5)) };
            shadow.AddThemeStyleboxOverride("panel", UiKit.Box(Palette.PaperShade.Darkened(0.04f * i), 22, border: Palette.PaperShade.Darkened(0.12f), borderWidth: 2));
            shadow.Name = $"Shadow{i}";
            _stackShadows.AddChild(shadow);
        }

        var card = UiKit.Panel(radius: 22, padH: 6, padV: 6);
        card.Position = new Vector2(0, -200);
        card.CustomMinimumSize = new Vector2(200, 200);
        card.Size = new Vector2(200, 200);
        anchor.AddChild(card);
        _stackCard = card;
        _handPreview = new TilePreview(_tileFactory, 188);
        card.AddChild(_handPreview);

        var count = new PanelContainer { MouseFilter = MouseFilterEnum.Ignore };
        count.AddThemeStyleboxOverride("panel", UiKit.Box(Palette.Ink, 18, padH: 14, padV: 2, shadow: 4));
        _tilesLeftLabel = UiKit.Text("0", 26, Colors.White, UiKit.Black, HorizontalAlignment.Center);
        count.AddChild(_tilesLeftLabel);
        count.Position = new Vector2(150, -222);
        anchor.AddChild(count);

        var nextCard = UiKit.Panel(radius: 16, padH: 4, padV: 4);
        nextCard.Position = new Vector2(222, -112);
        nextCard.CustomMinimumSize = new Vector2(96, 96);
        anchor.AddChild(nextCard);
        var nextCol = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        nextCol.AddThemeConstantOverride("separation", -6);
        nextCard.AddChild(nextCol);
        nextCol.AddChild(UiKit.Text("NEXT", 12, Palette.InkSoft, UiKit.Black, HorizontalAlignment.Center));
        _nextPreview = new TilePreview(_tileFactory, 84, 4.9f) { Spin = false };
        nextCol.AddChild(_nextPreview);

        _controlsHint = UiKit.Text("Click to place  ·  Q / E or right-click to rotate  ·  Wheel zoom  ·  Drag to look around",
            15, new Color(Palette.Paper, 0.95f), UiKit.Bold, outline: 6, outlineColor: new Color(Palette.Ink, 0.5f));
        _controlsHint.SetAnchorsPreset(LayoutPreset.CenterBottom);
        _controlsHint.GrowHorizontal = GrowDirection.Both;
        _controlsHint.Position = new Vector2(0, -38);
        AddChild(_controlsHint);
    }

    private void BuildBanner()
    {
        _banner = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore, Alignment = BoxContainer.AlignmentMode.Center };
        _banner.SetAnchorsPreset(LayoutPreset.Center);
        _banner.GrowHorizontal = GrowDirection.Both;
        _banner.GrowVertical = GrowDirection.Both;
        _banner.Position = new Vector2(0, -250);
        _banner.Modulate = new Color(1, 1, 1, 0);
        AddChild(_banner);
        _bannerTitle = UiKit.Text("", 64, Colors.White, UiKit.Black, HorizontalAlignment.Center, 14, Palette.Ink);
        _bannerSubtitle = UiKit.Text("", 24, Colors.White, UiKit.Bold, HorizontalAlignment.Center, 8, Palette.Ink);
        _banner.AddChild(_bannerTitle);
        _banner.AddChild(_bannerSubtitle);
    }

    private void BuildToast()
    {
        _toast = UiKit.Panel(new Color(Palette.Ink, 0.88f), 16, 22, 12);
        _toast.SetAnchorsPreset(LayoutPreset.CenterBottom);
        _toast.GrowHorizontal = GrowDirection.Both;
        _toast.GrowVertical = GrowDirection.Begin;
        _toast.Position = new Vector2(0, -80);
        _toast.Modulate = new Color(1, 1, 1, 0);
        _toast.MouseFilter = MouseFilterEnum.Ignore;
        _toastLabel = UiKit.Text("", 19, Palette.Paper, UiKit.Bold, HorizontalAlignment.Center);
        _toast.AddChild(_toastLabel);
        AddChild(_toast);
    }

    private void BuildHintPanel()
    {
        var panel = UiKit.Panel(new Color(Palette.Paper, 0.96f), 18, 20, 14);
        panel.SetAnchorsPreset(LayoutPreset.CenterRight);
        panel.GrowHorizontal = GrowDirection.Begin;
        panel.GrowVertical = GrowDirection.Both;
        panel.Position = new Vector2(-24, 0);
        panel.CustomMinimumSize = new Vector2(320, 0);
        panel.MouseFilter = MouseFilterEnum.Ignore;
        panel.Modulate = new Color(1, 1, 1, 0);
        var col = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        col.AddThemeConstantOverride("separation", 4);
        panel.AddChild(col);
        col.AddChild(UiKit.Text("TIP", 13, Palette.AccentDeep, UiKit.Black));
        _hintLabel = UiKit.Text("", 18, Palette.Ink, UiKit.Regular);
        _hintLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _hintLabel.CustomMinimumSize = new Vector2(300, 0);
        col.AddChild(_hintLabel);
        _hintPanel = panel;
        AddChild(panel);
    }

    // ------------------------------------------------------------------ state

    public void SetScore(long score, bool instant = false)
    {
        _targetScore = score;
        if (instant)
            _displayScore = score;
    }

    public void BumpScore() => _scorePulse = 1f;

    public void SetBest(long best) => _bestLabel.Text = $"Best {UiKit.Number(best)}";

    public void SetMilestone(long score, long previous, long next, Terrain? unlock, int tiles)
    {
        var span = Math.Max(1, next - previous);
        _milestoneTarget = Mathf.Clamp((float)(score - previous) / span, 0f, 1f);
        var reward = unlock is Terrain t ? $"unlocks {Terrains.DisplayName(t)}" : $"+{tiles} tiles";
        _milestoneLabel.Text = $"{UiKit.Number(next - score)} to next milestone  ·  {reward}";
    }

    public void ResetMilestoneBar() => _milestoneDisplay = 0f;

    public void SetStreak(int streak, double multiplier)
    {
        var show = streak > 0;
        if (show && !_streakPill.Visible)
        {
            _streakPill.Visible = true;
            _streakPill.Scale = Vector2.Zero;
        }

        _streakPill.Visible = show;
        if (!show)
            return;
        _streakLabel.Text = "×" + multiplier.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture);
        var heat = Mathf.Clamp(streak / 10f, 0f, 1f);
        _streakPill.AddThemeStyleboxOverride("panel", UiKit.Box(Palette.Accent.Lerp(new Color("e8553f"), heat), 14, padH: 12, padV: 2));
        _streakPill.PivotOffset = _streakPill.Size / 2f;
        var tween = CreateTween().SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
        _streakPill.Scale = Vector2.One * 1.35f;
        tween.TweenProperty(_streakPill, "scale", Vector2.One, 0.35);
    }

    public void SetUndo(int remaining, bool canUndo)
    {
        _undoButton.Text = $"↶ {remaining}";
        _undoButton.Disabled = !canUndo;
    }

    public void SetStack(TileSpec? current, TileSpec? next, int tilesLeft, int rotation)
    {
        _handPreview.SetTile(current, rotation);
        _nextPreview.SetTile(next, 0);
        _nextPreview.GetParent().GetParent<Control>().Visible = next is not null;
        if (tilesLeft != _lastTilesLeft)
        {
            var gained = _lastTilesLeft >= 0 && tilesLeft > _lastTilesLeft;
            _lastTilesLeft = tilesLeft;
            _tilesLeftLabel.Text = tilesLeft.ToString();
            var color = tilesLeft <= 5 ? Palette.Bad : Palette.Ink;
            _tilesLeftLabel.GetParent<PanelContainer>().AddThemeStyleboxOverride("panel", UiKit.Box(gained ? Palette.Good.Darkened(0.1f) : color, 18, padH: 14, padV: 2, shadow: 4));
            var pill = _tilesLeftLabel.GetParent<Control>();
            pill.PivotOffset = pill.Size / 2f;
            pill.Scale = Vector2.One * (gained ? 1.4f : 1.15f);
            var tween = CreateTween().SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
            tween.TweenProperty(pill, "scale", Vector2.One, 0.4);
            if (gained)
                tween.TweenCallback(Callable.From(() => pill.AddThemeStyleboxOverride("panel", UiKit.Box(Palette.Ink, 18, padH: 14, padV: 2, shadow: 4))));

            for (var i = 1; i <= 3; i++)
                _stackShadows.GetNode<Control>($"Shadow{i}").Visible = tilesLeft > i;
        }
    }

    public void SetHandRotation(int rotation) => _handPreview.SetRotation(rotation);

    public void SetQuests(IReadOnlyList<QuestStatus> quests)
    {
        _questHeader.Visible = quests.Count > 0;
        var ids = quests.Select(q => q.Quest.Id).ToHashSet();
        foreach (var id in _questCards.Keys.Where(id => !ids.Contains(id)).ToList())
        {
            var card = _questCards[id];
            _questCards.Remove(id);
            var tween = card.Root.CreateTween().SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.In);
            tween.TweenProperty(card.Root, "scale", new Vector2(0.01f, 0.01f), 0.25);
            tween.TweenCallback(Callable.From(card.Root.QueueFree));
        }

        foreach (var status in quests)
        {
            if (!_questCards.TryGetValue(status.Quest.Id, out var card))
            {
                card = new QuestCard(status.Quest);
                _questCards[status.Quest.Id] = card;
                _questList.AddChild(card.Root);
                card.Root.Scale = new Vector2(0.01f, 1f);
                var tween = card.Root.CreateTween().SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
                tween.TweenProperty(card.Root, "scale", Vector2.One, 0.4);
            }

            card.Update(status);
        }
    }

    public void SetHintVisible(bool visible) => _hintButton.Disabled = !visible;

    public void SetControlsHintVisible(bool visible) => _controlsHint.Visible = visible;

    // ------------------------------------------------------------------ quest pins in the world

    public void UpdateQuestPins(IEnumerable<(int Id, Vector2? Screen, QuestStatus Status)> pins)
    {
        var seen = new HashSet<int>();
        foreach (var (id, screen, status) in pins)
        {
            seen.Add(id);
            if (!_questPins.TryGetValue(id, out var pin))
            {
                pin = BuildPin(status);
                _questPins[id] = pin;
                _pinLayer.AddChild(pin);
                pin.Scale = Vector2.Zero;
                var tween = pin.CreateTween().SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
                tween.TweenProperty(pin, "scale", Vector2.One, 0.45);
            }

            pin.Visible = screen is not null;
            if (screen is Vector2 s)
            {
                pin.Position = pin.Position.Lerp(s - new Vector2(pin.Size.X / 2f, pin.Size.Y + 8), pin.Position == Vector2.Zero ? 1f : 0.5f);
                pin.PivotOffset = new Vector2(pin.Size.X / 2f, pin.Size.Y);
            }

            var label = pin.GetNode<Label>("Row/Count");
            label.Text = status.Quest.Kind == QuestKind.Exactly
                ? $"{status.CurrentSize} = {status.Quest.Target}"
                : $"{status.CurrentSize} / {status.Quest.Target}";
        }

        foreach (var id in _questPins.Keys.Where(id => !seen.Contains(id)).ToList())
        {
            var pin = _questPins[id];
            _questPins.Remove(id);
            var tween = pin.CreateTween().SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.In);
            tween.TweenProperty(pin, "scale", Vector2.Zero, 0.25);
            tween.TweenCallback(Callable.From(pin.QueueFree));
        }
    }

    private static Control BuildPin(QuestStatus status)
    {
        var color = Palette.TerrainSwatch(status.Quest.Terrain);
        var panel = new PanelContainer { MouseFilter = MouseFilterEnum.Ignore };
        panel.AddThemeStyleboxOverride("panel", UiKit.Box(new Color(Palette.Paper, 0.95f), 16, border: color, borderWidth: 3, shadow: 5, padH: 8, padV: 4));
        var row = new HBoxContainer { Name = "Row", MouseFilter = MouseFilterEnum.Ignore };
        row.AddThemeConstantOverride("separation", 6);
        panel.AddChild(row);
        row.AddChild(UiKit.Badge(Palette.TerrainGlyph(status.Quest.Terrain), color, 26, 15));
        var count = UiKit.Text("", 17, Palette.Ink, UiKit.Black);
        count.Name = "Count";
        row.AddChild(count);
        return panel;
    }

    // ------------------------------------------------------------------ juice

    public void ShowPopup(Vector2 screen, string text, Color color, int size = 30, float rise = 70f, float duration = 1.1f, float delay = 0f)
    {
        var label = UiKit.Text(text, size, color, UiKit.Black, HorizontalAlignment.Center, Math.Max(6, size / 4), Palette.Ink);
        label.Modulate = new Color(1, 1, 1, 0);
        _popupLayer.AddChild(label);
        label.ResetSize();
        label.Position = screen - (label.Size / 2f);
        label.PivotOffset = label.Size / 2f;
        label.Scale = Vector2.One * 0.4f;

        var tween = label.CreateTween();
        tween.TweenInterval(delay);
        tween.TweenProperty(label, "modulate:a", 1f, 0.08);
        tween.Parallel().TweenProperty(label, "scale", Vector2.One * 1.15f, 0.18).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
        tween.TweenProperty(label, "scale", Vector2.One, 0.12);
        tween.Parallel().TweenProperty(label, "position:y", label.Position.Y - rise, duration).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
        tween.Parallel().TweenProperty(label, "modulate:a", 0f, 0.35).SetDelay(duration * 0.65);
        tween.TweenCallback(Callable.From(label.QueueFree));
    }

    /// <summary>A "+N" chip that arcs from a tile to the score counter and bumps it on arrival.</summary>
    public void FlyPoints(Vector2 from, string text, Color color, float delay, Action onArrive)
    {
        var chip = new PanelContainer { MouseFilter = MouseFilterEnum.Ignore };
        chip.AddThemeStyleboxOverride("panel", UiKit.Box(color, 14, padH: 10, padV: 2, shadow: 4));
        chip.AddChild(UiKit.Text(text, 20, Colors.White, UiKit.Black, HorizontalAlignment.Center));
        chip.Modulate = new Color(1, 1, 1, 0);
        _popupLayer.AddChild(chip);
        chip.ResetSize();
        var start = from - (chip.Size / 2f);
        chip.Position = start;
        var end = ScoreAnchor - (chip.Size / 2f);
        var control = (start + end) / 2f + new Vector2(0, -160);

        var tween = chip.CreateTween();
        tween.TweenInterval(delay);
        tween.TweenProperty(chip, "modulate:a", 1f, 0.1);
        tween.TweenMethod(Callable.From<float>(t =>
        {
            var e = Ease.InOutSine(t);
            var a = start.Lerp(control, e);
            var b = control.Lerp(end, e);
            chip.Position = a.Lerp(b, e);
            chip.Scale = Vector2.One * Mathf.Lerp(1f, 0.6f, e);
        }), 0f, 1f, 0.6);
        tween.TweenCallback(Callable.From(() =>
        {
            onArrive();
            chip.QueueFree();
        }));
    }

    /// <summary>Tiles won fly from the board to the stack card.</summary>
    public void FlyTiles(Vector2 from, int amount, float delay)
    {
        for (var i = 0; i < Math.Min(amount, 6); i++)
        {
            var chip = new Panel { MouseFilter = MouseFilterEnum.Ignore, Size = new Vector2(30, 30) };
            chip.AddThemeStyleboxOverride("panel", UiKit.Box(Palette.Good, 8, border: Colors.White, borderWidth: 2, shadow: 3));
            chip.Modulate = new Color(1, 1, 1, 0);
            chip.PivotOffset = new Vector2(15, 15);
            _popupLayer.AddChild(chip);
            var start = from + new Vector2((i - (amount / 2f)) * 18f, 0) - new Vector2(15, 15);
            var end = StackAnchor - new Vector2(15, 15);
            chip.Position = start;
            var control = (start + end) / 2f + new Vector2(-60, -120);
            var tween = chip.CreateTween();
            tween.TweenInterval(delay + (i * 0.07f));
            tween.TweenProperty(chip, "modulate:a", 1f, 0.08);
            tween.TweenMethod(Callable.From<float>(t =>
            {
                var e = Ease.InOutSine(t);
                chip.Position = start.Lerp(control, e).Lerp(control.Lerp(end, e), e);
                chip.Rotation = e * 3f;
            }), 0f, 1f, 0.7);
            tween.TweenCallback(Callable.From(() =>
            {
                _handPreview.Bump();
                chip.QueueFree();
            }));
        }
    }

    public void Banner(string title, string subtitle, Color color, float hold = 1.1f)
    {
        _bannerTween?.Kill();
        _bannerTitle.Text = title;
        _bannerTitle.LabelSettings.FontColor = color;
        _bannerSubtitle.Text = subtitle;
        _bannerSubtitle.Visible = !string.IsNullOrEmpty(subtitle);
        _banner.PivotOffset = _banner.Size / 2f;
        _banner.Scale = Vector2.One * 0.6f;
        _banner.Modulate = new Color(1, 1, 1, 0);
        _banner.Rotation = -0.06f;
        _bannerTween = _banner.CreateTween();
        _bannerTween.TweenProperty(_banner, "modulate:a", 1f, 0.12);
        _bannerTween.Parallel().TweenProperty(_banner, "scale", Vector2.One, 0.45).SetTrans(Tween.TransitionType.Elastic).SetEase(Tween.EaseType.Out);
        _bannerTween.Parallel().TweenProperty(_banner, "rotation", 0f, 0.35).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
        _bannerTween.TweenInterval(hold);
        _bannerTween.TweenProperty(_banner, "modulate:a", 0f, 0.35);
        _bannerTween.Parallel().TweenProperty(_banner, "position:y", _banner.Position.Y - 30, 0.35);
        _bannerTween.TweenCallback(Callable.From(() => _banner.Position = new Vector2(_banner.Position.X, _banner.Position.Y + 30)));
    }

    public void Toast(string text, float duration = 2.6f)
    {
        _toastLabel.Text = text;
        _toastTimer = duration;
        _toast.ResetSize();
        var tween = _toast.CreateTween();
        tween.TweenProperty(_toast, "modulate:a", 1f, 0.2);
    }

    public void ShowTip(string? text)
    {
        var tween = _hintPanel.CreateTween();
        if (string.IsNullOrEmpty(text))
        {
            tween.TweenProperty(_hintPanel, "modulate:a", 0f, 0.25);
            return;
        }

        if (_hintLabel.Text == text && _hintPanel.Modulate.A > 0.5f)
            return;
        _hintLabel.Text = text;
        _hintPanel.Modulate = new Color(1, 1, 1, 0);
        _hintPanel.Position = new Vector2(_hintPanel.Position.X + 40, _hintPanel.Position.Y);
        tween.TweenProperty(_hintPanel, "modulate:a", 1f, 0.3);
        tween.Parallel().TweenProperty(_hintPanel, "position:x", _hintPanel.Position.X - 40, 0.35).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
    }

    public override void _Process(double delta)
    {
        var dt = (float)delta;
        if (Math.Abs(_displayScore - _targetScore) > 0.5)
        {
            var step = Math.Max(1.0, Math.Abs(_targetScore - _displayScore) * Ease.Damp(7f, dt));
            _displayScore += Math.Sign(_targetScore - _displayScore) * Math.Min(step, Math.Abs(_targetScore - _displayScore));
        }
        else
        {
            _displayScore = _targetScore;
        }

        _scoreLabel.Text = UiKit.Number((long)Math.Round(_displayScore));
        _scorePulse = Mathf.Max(0f, _scorePulse - (dt * 4f));
        _scoreBox.PivotOffset = _scoreBox.Size / 2f;
        _scoreBox.Scale = Vector2.One * (1f + (Ease.OutBack(_scorePulse) * 0.0f) + (_scorePulse * 0.18f));

        _milestoneDisplay = Mathf.Lerp(_milestoneDisplay, _milestoneTarget, Ease.Damp(6f, dt));
        _milestoneBar.Value = _milestoneDisplay;

        if (_toastTimer > 0f)
        {
            _toastTimer -= dt;
            if (_toastTimer <= 0f)
                _toast.CreateTween().TweenProperty(_toast, "modulate:a", 0f, 0.3);
        }
    }

    // ------------------------------------------------------------------ quest card

    private sealed class QuestCard
    {
        public readonly PanelContainer Root;
        private readonly Label _title;
        private readonly Label _count;
        private readonly ProgressBar _bar;
        private readonly Label _reward;
        private int _lastSize = -1;

        public QuestCard(Quest quest)
        {
            var color = Palette.TerrainSwatch(quest.Terrain);
            Root = UiKit.Panel(padH: 12, padV: 10, radius: 16, shadow: 5);
            Root.CustomMinimumSize = new Vector2(272, 0);
            Root.MouseFilter = MouseFilterEnum.Ignore;
            var row = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
            row.AddThemeConstantOverride("separation", 10);
            Root.AddChild(row);
            row.AddChild(UiKit.Badge(Palette.TerrainGlyph(quest.Terrain), color, 42, 22));
            var col = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore, SizeFlagsHorizontal = SizeFlags.ExpandFill };
            col.AddThemeConstantOverride("separation", 2);
            row.AddChild(col);
            var top = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
            col.AddChild(top);
            var verb = quest.Kind == QuestKind.Exactly ? "Exactly" : "Grow";
            _title = UiKit.Text($"{verb} {Terrains.Info(quest.Terrain).QuestNoun}", 17, Palette.Ink, UiKit.Bold);
            _title.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            top.AddChild(_title);
            _count = UiKit.Text("", 17, Palette.Ink, UiKit.Black, HorizontalAlignment.Right);
            top.AddChild(_count);
            _bar = UiKit.Bar(color, 8);
            col.AddChild(_bar);
            _reward = UiKit.Text($"+{quest.RewardPoints} pts  ·  +{quest.RewardTiles} tiles", 13, Palette.InkSoft, UiKit.Bold);
            col.AddChild(_reward);
        }

        public void Update(QuestStatus status)
        {
            var q = status.Quest;
            _count.Text = q.Kind == QuestKind.Exactly ? $"{status.CurrentSize} → {q.Target}" : $"{status.CurrentSize}/{q.Target}";
            var target = Mathf.Clamp(status.CurrentSize / (float)q.Target, 0f, 1f);
            var tween = _bar.CreateTween().SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
            tween.TweenProperty(_bar, "value", target, 0.5);
            if (_lastSize >= 0 && status.CurrentSize != _lastSize)
            {
                Root.PivotOffset = Root.Size / 2f;
                Root.Scale = Vector2.One * 1.06f;
                Root.CreateTween().SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out).TweenProperty(Root, "scale", Vector2.One, 0.3);
            }

            _lastSize = status.CurrentSize;
            if (q.Kind == QuestKind.Exactly && status.CurrentSize > q.Target - 3)
                _count.LabelSettings.FontColor = Palette.AccentDeep;
        }
    }
}
