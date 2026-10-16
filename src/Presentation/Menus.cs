using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using LandBuilder.Domain;
using LandBuilder.Infrastructure;

namespace LandBuilder.Presentation;

/// <summary>Title, pause, settings, how-to-play and run-summary screens.</summary>
public partial class Menus : Control
{
    private readonly Audio _audio;
    private readonly GameSettings _settings;
    private Control _title = null!;
    private Control _pause = null!;
    private Control _settingsScreen = null!;
    private Control _howTo = null!;
    private Control _gameOver = null!;
    private ColorRect _dim = null!;
    private Button _continueButton = null!;
    private VBoxContainer _leaderboard = null!;
    private Control? _returnTo;

    // Game-over widgets
    private Label _finalScore = null!;
    private Label _newBest = null!;
    private GridContainer _statsGrid = null!;
    private Label _rankLabel = null!;
    private Tween? _countTween;

    public event Action? ContinuePressed;
    public event Action? NewGamePressed;
    public event Action? ResumePressed;
    public event Action? RestartPressed;
    public event Action? MainMenuPressed;
    public event Action? QuitPressed;
    public event Action? SettingsChanged;

    public Menus(Audio audio, GameSettings settings)
    {
        _audio = audio;
        _settings = settings;
        SetAnchorsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Ignore;
    }

    public bool IsAnyOpen => _title.Visible || _pause.Visible || _settingsScreen.Visible || _howTo.Visible || _gameOver.Visible;
    public bool IsTitleOpen => _title.Visible;
    public bool IsGameOverOpen => _gameOver.Visible;

    public override void _Ready()
    {
        _dim = new ColorRect { Color = new Color(Palette.Ink, 0.35f), MouseFilter = MouseFilterEnum.Stop, Visible = false };
        _dim.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(_dim);

        _title = BuildTitle();
        _pause = BuildPause();
        _settingsScreen = BuildSettings();
        _howTo = BuildHowTo();
        _gameOver = BuildGameOver();
        foreach (var screen in new[] { _title, _pause, _settingsScreen, _howTo, _gameOver })
        {
            screen.Visible = false;
            AddChild(screen);
        }
    }

    // ------------------------------------------------------------------ navigation

    private void Show(Control screen, bool dim = true)
    {
        foreach (var s in new[] { _title, _pause, _settingsScreen, _howTo, _gameOver })
            s.Visible = s == screen;
        _dim.Visible = dim;
        _dim.Color = new Color(Palette.Ink, screen == _title ? 0.0f : 0.35f);
        screen.Modulate = new Color(1, 1, 1, 0);
        screen.PivotOffset = screen.Size / 2f;
        screen.Scale = Vector2.One * 0.96f;
        var tween = screen.CreateTween().SetParallel();
        tween.TweenProperty(screen, "modulate:a", 1f, 0.22);
        tween.TweenProperty(screen, "scale", Vector2.One, 0.3).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
    }

    public void HideAll()
    {
        foreach (var s in new[] { _title, _pause, _settingsScreen, _howTo, _gameOver })
            s.Visible = false;
        _dim.Visible = false;
    }

    public void ShowTitle(bool canContinue, IReadOnlyList<RunRecord> leaderboard)
    {
        _continueButton.Visible = canContinue;
        FillLeaderboard(leaderboard);
        Show(_title, dim: false);
        _dim.Visible = false;
    }

    public void ShowPause() => Show(_pause);

    /// <summary>Back out of a sub-screen or close the pause menu. Returns false if nothing handled it.</summary>
    public bool Back()
    {
        if (_settingsScreen.Visible || _howTo.Visible)
        {
            if (_returnTo is not null)
            {
                if (_returnTo == _title)
                    Show(_title, dim: false);
                else
                    Show(_returnTo);
            }

            return true;
        }

        if (_pause.Visible)
        {
            ResumePressed?.Invoke();
            return true;
        }

        return false;
    }

    public void OpenSettings() => OpenSub(_settingsScreen);

    public void OpenHowTo() => OpenSub(_howTo);

    private void OpenSub(Control screen)
    {
        _returnTo = _title.Visible ? _title : _pause.Visible ? _pause : _gameOver.Visible ? _gameOver : null;
        Show(screen);
    }

    // ------------------------------------------------------------------ screens

    private Control Centered(Control content)
    {
        var center = new CenterContainer { MouseFilter = MouseFilterEnum.Ignore };
        center.SetAnchorsPreset(LayoutPreset.FullRect);
        center.AddChild(content);
        return center;
    }

    private Control BuildTitle()
    {
        var root = new Control { MouseFilter = MouseFilterEnum.Ignore };
        root.SetAnchorsPreset(LayoutPreset.FullRect);

        var left = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        left.SetAnchorsPreset(LayoutPreset.CenterLeft);
        left.GrowVertical = GrowDirection.Both;
        left.Position = new Vector2(90, 0);
        left.AddThemeConstantOverride("separation", 14);
        root.AddChild(left);

        var title = UiKit.Text("Land Builder", 92, Colors.White, UiKit.Black, outline: 22, outlineColor: new Color("2a5a3a"));
        title.LabelSettings.ShadowColor = new Color(0, 0, 0, 0.25f);
        title.LabelSettings.ShadowOffset = new Vector2(0, 6);
        title.LabelSettings.ShadowSize = 6;
        left.AddChild(title);
        left.AddChild(UiKit.Text("Grow a cozy island, one tile at a time.", 26, Colors.White, UiKit.Bold, outline: 10, outlineColor: new Color(Palette.Ink, 0.55f)));
        left.AddChild(UiKit.Spacer(0, 24));

        _continueButton = UiKit.Button("Continue", () => ContinuePressed?.Invoke(), _audio, 300, primary: true);
        left.AddChild(_continueButton);
        left.AddChild(UiKit.Button("New Island", () => NewGamePressed?.Invoke(), _audio, 300, primary: true));
        left.AddChild(UiKit.Button("How to Play", () => OpenSub(_howTo), _audio, 300));
        left.AddChild(UiKit.Button("Settings", () => OpenSub(_settingsScreen), _audio, 300));
        left.AddChild(UiKit.Button("Quit", () => QuitPressed?.Invoke(), _audio, 300));

        var boardPanel = UiKit.Panel(padH: 24, padV: 18);
        boardPanel.SetAnchorsPreset(LayoutPreset.BottomRight);
        boardPanel.GrowHorizontal = GrowDirection.Begin;
        boardPanel.GrowVertical = GrowDirection.Begin;
        boardPanel.Position = new Vector2(-40, -40);
        boardPanel.CustomMinimumSize = new Vector2(320, 0);
        root.AddChild(boardPanel);
        var col = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        col.AddThemeConstantOverride("separation", 8);
        boardPanel.AddChild(col);
        col.AddChild(UiKit.Text("BEST ISLANDS", 16, Palette.AccentDeep, UiKit.Black));
        _leaderboard = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        _leaderboard.AddThemeConstantOverride("separation", 4);
        col.AddChild(_leaderboard);
        return root;
    }

    private void FillLeaderboard(IReadOnlyList<RunRecord> runs)
    {
        foreach (var child in _leaderboard.GetChildren())
            child.QueueFree();

        if (runs.Count == 0)
        {
            _leaderboard.AddChild(UiKit.Text("No islands yet. Your first one awaits!", 17, Palette.InkSoft));
            return;
        }

        var rank = 1;
        foreach (var run in runs.Take(6))
        {
            var row = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
            row.AddThemeConstantOverride("separation", 10);
            row.AddChild(UiKit.Text($"{rank}.", 18, rank == 1 ? Palette.AccentDeep : Palette.InkSoft, UiKit.Black));
            var score = UiKit.Text(UiKit.Number(run.Score), 20, Palette.Ink, UiKit.Black);
            score.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            row.AddChild(score);
            row.AddChild(UiKit.Text($"{run.TilesPlaced} tiles", 15, Palette.InkSoft, UiKit.Bold));
            _leaderboard.AddChild(row);
            rank++;
        }
    }

    private Control BuildPause()
    {
        var panel = UiKit.Panel(padH: 40, padV: 30);
        var col = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        col.AddThemeConstantOverride("separation", 12);
        panel.AddChild(col);
        col.AddChild(UiKit.Text("Paused", 44, Palette.Ink, UiKit.Black, HorizontalAlignment.Center));
        col.AddChild(UiKit.Spacer(0, 6));
        col.AddChild(UiKit.Button("Resume", () => ResumePressed?.Invoke(), _audio, 300, primary: true));
        col.AddChild(UiKit.Button("How to Play", () => OpenSub(_howTo), _audio, 300));
        col.AddChild(UiKit.Button("Settings", () => OpenSub(_settingsScreen), _audio, 300));
        col.AddChild(UiKit.Button("Restart Island", () => RestartPressed?.Invoke(), _audio, 300));
        col.AddChild(UiKit.Button("Save & Main Menu", () => MainMenuPressed?.Invoke(), _audio, 300));
        return Centered(panel);
    }

    private Control BuildSettings()
    {
        var panel = UiKit.Panel(padH: 40, padV: 30);
        var col = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        col.AddThemeConstantOverride("separation", 10);
        col.CustomMinimumSize = new Vector2(480, 0);
        panel.AddChild(col);
        col.AddChild(UiKit.Text("Settings", 40, Palette.Ink, UiKit.Black, HorizontalAlignment.Center));

        col.AddChild(Section("AUDIO"));
        col.AddChild(Slider("Master", _settings.MasterVolume, v => _settings.MasterVolume = v));
        col.AddChild(Slider("Music", _settings.MusicVolume, v => _settings.MusicVolume = v));
        col.AddChild(Slider("Effects", _settings.SfxVolume, v => _settings.SfxVolume = v));
        col.AddChild(Slider("Ocean ambience", _settings.AmbientVolume, v => _settings.AmbientVolume = v));

        col.AddChild(Section("VISUALS"));
        var grid = new GridContainer { Columns = 2, MouseFilter = MouseFilterEnum.Ignore };
        grid.AddThemeConstantOverride("h_separation", 24);
        grid.AddThemeConstantOverride("v_separation", 6);
        col.AddChild(grid);
        grid.AddChild(Toggle("Shadows", _settings.Shadows, v => _settings.Shadows = v));
        grid.AddChild(Toggle("Ambient occlusion", _settings.AmbientOcclusion, v => _settings.AmbientOcclusion = v));
        grid.AddChild(Toggle("Bloom", _settings.Bloom, v => _settings.Bloom = v));
        grid.AddChild(Toggle("Tilt-shift focus", _settings.DepthOfField, v => _settings.DepthOfField = v));
        grid.AddChild(Toggle("Particles", _settings.Particles, v => _settings.Particles = v));
        grid.AddChild(Toggle("Screen shake", _settings.ScreenShake, v => _settings.ScreenShake = v));
        grid.AddChild(Toggle("Edge match hints", _settings.EdgeHints, v => _settings.EdgeHints = v));
        grid.AddChild(Toggle("Fullscreen", _settings.Fullscreen, v => _settings.Fullscreen = v));

        col.AddChild(UiKit.Spacer(0, 8));
        var back = UiKit.Button("Back", () => Back(), _audio, 300, primary: true);
        back.SizeFlagsHorizontal = SizeFlags.ShrinkCenter;
        col.AddChild(back);
        return Centered(panel);
    }

    private static Label Section(string text) => UiKit.Text(text, 14, Palette.AccentDeep, UiKit.Black);

    private Control Slider(string label, float value, Action<float> set)
    {
        var row = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        row.AddThemeConstantOverride("separation", 16);
        var name = UiKit.Text(label, 18, Palette.Ink, UiKit.Bold);
        name.CustomMinimumSize = new Vector2(170, 0);
        row.AddChild(name);
        var slider = new HSlider { MinValue = 0, MaxValue = 1, Step = 0.01, Value = value, SizeFlagsHorizontal = SizeFlags.ExpandFill, CustomMinimumSize = new Vector2(240, 30), FocusMode = FocusModeEnum.None };
        slider.ValueChanged += v =>
        {
            set((float)v);
            SettingsChanged?.Invoke();
        };
        slider.DragEnded += _ =>
        {
            _audio.Play(Sfx.UiClick);
            _settings.Save();
        };
        row.AddChild(slider);
        return row;
    }

    private Control Toggle(string label, bool value, Action<bool> set)
    {
        var check = new CheckBox { Text = label, ButtonPressed = value, FocusMode = FocusModeEnum.None, MouseDefaultCursorShape = CursorShape.PointingHand };
        check.AddThemeFontOverride("font", UiKit.Bold);
        check.AddThemeFontSizeOverride("font_size", 18);
        check.Toggled += on =>
        {
            _audio.Play(Sfx.UiClick);
            set(on);
            _settings.Save();
            SettingsChanged?.Invoke();
        };
        return check;
    }

    private Control BuildHowTo()
    {
        var panel = UiKit.Panel(padH: 40, padV: 30);
        var col = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        col.AddThemeConstantOverride("separation", 12);
        col.CustomMinimumSize = new Vector2(640, 0);
        panel.AddChild(col);
        col.AddChild(UiKit.Text("How to Play", 40, Palette.Ink, UiKit.Black, HorizontalAlignment.Center));

        (IconKind Glyph, Color Color, string Title, string Body)[] rules =
        [
            (IconKind.Hex, Palette.TerrainSwatch(Terrain.Grass), "Lay tiles", "Each tile has six edges. Place it next to your island; rotate with Q / E, the mouse wheel while holding Shift, or a right-click."),
            (IconKind.Check, Palette.Good, "Match edges", "Every edge that touches the same terrain scores 10 points. Matching edges light up green before you place."),
            (IconKind.Star, Palette.AccentDeep, "Perfect fits", "Touch 3+ tiles with every edge matching for a Perfect: bonus points and a free tile. Fill a hole with all six matching for a Flawless!"),
            (IconKind.Streak, new Color("e8553f"), "Streaks", "Clean fits against two or more tiles grow your streak multiplier (up to ×2). A mismatched edge resets it."),
            (IconKind.Flag, Palette.TerrainSwatch(Terrain.Forest), "Quests", "Grow the marked forest, field, village or lake to the target size for points and extra tiles. 'Exactly' quests fail if you overshoot; any quest fails if its area gets closed in."),
            (IconKind.Mountain, Palette.TerrainSwatch(Terrain.Mountain), "Milestones", "Reaching score milestones adds tiles and unlocks Mountains and Dunes."),
            (IconKind.Stack, Palette.Ink, "Your stack", "The run ends when the stack runs out, so keep it alive with perfects, quests and milestones. You have 3 undos per island.")
        ];

        foreach (var (glyph, color, title, body) in rules)
        {
            var row = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
            row.AddThemeConstantOverride("separation", 14);
            row.AddChild(UiKit.Badge(glyph, color, 44));
            var text = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore, SizeFlagsHorizontal = SizeFlags.ExpandFill };
            text.AddThemeConstantOverride("separation", 0);
            text.AddChild(UiKit.Text(title, 20, Palette.Ink, UiKit.Black));
            var b = UiKit.Text(body, 16, Palette.InkSoft);
            b.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            b.CustomMinimumSize = new Vector2(560, 0);
            text.AddChild(b);
            row.AddChild(text);
            col.AddChild(row);
        }

        col.AddChild(UiKit.Text("Camera: left-drag or WASD to pan, right-drag to orbit, wheel to zoom, F to re-centre. H shows a hint, Ctrl+Z undoes.", 15, Palette.InkSoft, UiKit.Bold, HorizontalAlignment.Center));
        var back = UiKit.Button("Got it", () => Back(), _audio, 260, primary: true);
        back.SizeFlagsHorizontal = SizeFlags.ShrinkCenter;
        col.AddChild(back);
        return Centered(panel);
    }

    private Control BuildGameOver()
    {
        var panel = UiKit.Panel(padH: 44, padV: 32);
        var col = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        col.AddThemeConstantOverride("separation", 10);
        col.CustomMinimumSize = new Vector2(520, 0);
        panel.AddChild(col);
        col.AddChild(UiKit.Text("Island complete!", 42, Palette.Ink, UiKit.Black, HorizontalAlignment.Center));
        col.AddChild(UiKit.Text("FINAL SCORE", 15, Palette.InkSoft, UiKit.Black, HorizontalAlignment.Center));
        _finalScore = UiKit.Text("0", 72, Palette.AccentDeep, UiKit.Black, HorizontalAlignment.Center);
        col.AddChild(_finalScore);
        _newBest = UiKit.Text("★ NEW BEST ★", 24, Palette.Good.Darkened(0.15f), UiKit.Black, HorizontalAlignment.Center);
        col.AddChild(_newBest);
        _rankLabel = UiKit.Text("", 16, Palette.InkSoft, UiKit.Bold, HorizontalAlignment.Center);
        col.AddChild(_rankLabel);

        _statsGrid = new GridContainer { Columns = 2, MouseFilter = MouseFilterEnum.Ignore, SizeFlagsHorizontal = SizeFlags.ShrinkCenter };
        _statsGrid.AddThemeConstantOverride("h_separation", 40);
        _statsGrid.AddThemeConstantOverride("v_separation", 4);
        col.AddChild(_statsGrid);

        col.AddChild(UiKit.Spacer(0, 10));
        var buttons = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore, Alignment = BoxContainer.AlignmentMode.Center };
        buttons.AddThemeConstantOverride("separation", 14);
        buttons.AddChild(UiKit.Button("New Island", () => NewGamePressed?.Invoke(), _audio, 220, primary: true));
        buttons.AddChild(UiKit.Button("Main Menu", () => MainMenuPressed?.Invoke(), _audio, 220));
        col.AddChild(buttons);

        // Sit beside the island (the camera shifts it to the right) so the player can admire what they built.
        var root = new Control { MouseFilter = MouseFilterEnum.Ignore };
        root.SetAnchorsPreset(LayoutPreset.FullRect);
        panel.SetAnchorsPreset(LayoutPreset.CenterLeft);
        panel.GrowVertical = GrowDirection.Both;
        panel.Position = new Vector2(90, 0);
        root.AddChild(panel);
        return root;
    }

    public void ShowGameOver(long score, RunStats stats, bool newBest, int rank)
    {
        foreach (var child in _statsGrid.GetChildren())
            child.QueueFree();

        void Stat(string name, string value)
        {
            _statsGrid.AddChild(UiKit.Text(name, 18, Palette.InkSoft, UiKit.Bold));
            _statsGrid.AddChild(UiKit.Text(value, 18, Palette.Ink, UiKit.Black, HorizontalAlignment.Right));
        }

        Stat("Tiles placed", stats.TilesPlaced.ToString());
        Stat("Perfect fits", stats.PerfectPlacements.ToString());
        Stat("Flawless fits", stats.FlawlessPlacements.ToString());
        Stat("Quests completed", $"{stats.QuestsCompleted}");
        Stat("Best streak", stats.BestStreak.ToString());
        Stat("Largest area", stats.LargestRegion > 0 ? $"{stats.LargestRegion} ({Terrains.DisplayName(stats.LargestRegionTerrain)})" : "-");

        _newBest.Visible = newBest;
        _rankLabel.Text = rank > 0 ? $"#{rank} on your best islands" : "";
        Show(_gameOver, dim: true);
        _dim.Color = new Color(Palette.Ink, 0.2f);

        _countTween?.Kill();
        _countTween = CreateTween();
        _countTween.TweenMethod(Callable.From<float>(v => _finalScore.Text = UiKit.Number((long)v)), 0f, (float)score, 1.6).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
        if (newBest)
        {
            _newBest.Modulate = new Color(1, 1, 1, 0);
            _countTween.TweenProperty(_newBest, "modulate:a", 1f, 0.3);
        }
    }
}
