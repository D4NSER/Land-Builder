using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using LandBuilder.Application;
using LandBuilder.Domain;
using LandBuilder.Infrastructure;

namespace LandBuilder.Presentation;

/// <summary>
/// Root of the game. Wires the rules (<see cref="GameSession"/>) to the world, HUD, menus and audio, and owns the
/// moment-to-moment feel: the floating hand tile, drop-and-land placement, and all the feedback that follows.
/// </summary>
public partial class Main : Node3D
{
    private enum Mode
    {
        Title,
        Playing,
        Paused,
        GameOver
    }

    private string SavePath = "user://save.json";
    private string ScoresPath = "user://highscores.json";
    private const float HoverHeight = 0.55f;

    private readonly SaveRepository _saves = new();
    private readonly HighScoreRepository _scores = new();
    private readonly InMemoryEventSink _sink = new();
    private readonly List<(long Score, double Due)> _scoreQueue = new();
    private readonly Dictionary<TileVisual, PendingFeedback> _pendingFeedback = new();
    private GameState? _questCacheState;
    private IReadOnlyList<QuestStatus> _questCache = [];

    private GameSettings _settings = null!;
    private WorldView _world = null!;
    private BoardView _board = null!;
    private Fx _fx = null!;
    private CameraRig _rig = null!;
    private Audio _audio = null!;
    private Hud _hud = null!;
    private Menus _menus = null!;
    private Node3D _handRoot = null!;

    private GameSession _session = null!;
    private Mode _mode = Mode.Title;
    private bool _attract;
    private bool _autoplay;
    private long _best;

    // Hand tile
    private TileVisual? _hand;
    private TileSpec? _handTile;
    private Spring3 _handPos = new(Vector3.Zero, 210f, 21f);
    private Spring _handScale = new(1f, 260f, 15f);
    private Spring _handHop = new(0f, 300f, 14f);
    private float _handYaw;
    private int _rotation;
    private int _rotationTurns;
    private int? _hoverSlot;
    private int? _lastHoverSlot;
    private PlacementEvaluation? _hoverEval;
    private float _handSpawnDelay;
    private bool _handHidden;

    // Input
    private bool _lmbDown;
    private bool _rmbDown;
    private bool _mmbDown;
    private Vector2 _pressPos;
    private bool _dragging;

    // Misc state
    private double _time;
    private int? _hintSlot;
    private float _hintTimer;
    private float _botTimer;
    private PlacementEvaluation? _botPlan;
    private float _gameOverDelay = -1f;
    private int _tutorialStage;
    private int _tutorialMark;
    private float _idleHintTimer;

    public override void _Ready()
    {
        _settings = GameSettings.Load();
        var args = OS.GetCmdlineUserArgs().Concat(OS.GetCmdlineArgs()).ToArray();
        _autoplay = args.Contains("--autoplay");
        _captureDir = args.FirstOrDefault(a => a.StartsWith("--capture-dir="))?.Split('=', 2)[1];
        _captureEvery = int.TryParse(args.FirstOrDefault(a => a.StartsWith("--capture-every="))?.Split('=', 2)[1], out var every) ? every : 90;
        _titleCapture = args.Contains("--title");
        _selfTest = args.Contains("--selftest");
        if (_selfTest || _autoplay)
        {
            // Never touch the player's real save or leaderboard from automated runs.
            SavePath = "user://automation/save.json";
            ScoresPath = "user://automation/highscores.json";
            System.IO.Directory.CreateDirectory(ProjectSettings.GlobalizePath("user://automation"));
        }

        _audio = new Audio { Name = "Audio" };
        AddChild(_audio);
        _world = new WorldView { Name = "World" };
        AddChild(_world);
        _fx = new Fx { Name = "Fx" };
        AddChild(_fx);
        _board = new BoardView { Name = "Board", Fx = _fx };
        AddChild(_board);
        _handRoot = new Node3D { Name = "Hand" };
        AddChild(_handRoot);
        _rig = new CameraRig { Name = "CameraRig" };
        AddChild(_rig);

        var canvas = new CanvasLayer { Name = "UI" };
        AddChild(canvas);
        var uiRoot = new Control { Theme = UiKit.BuildTheme(), MouseFilter = Control.MouseFilterEnum.Ignore };
        uiRoot.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        canvas.AddChild(uiRoot);
        _hud = new Hud(_audio, (tile, rotation) => _board.CreateTileVisual(tile, rotation, Vector3.Zero, 1234)) { Name = "Hud" };
        uiRoot.AddChild(_hud);
        _menus = new Menus(_audio, _settings) { Name = "Menus" };
        uiRoot.AddChild(_menus);

        _board.TileLanded += OnTileLanded;
        _hud.UndoPressed += Undo;
        _hud.HintPressed += ShowHint;
        _hud.PausePressed += Pause;
        _menus.ContinuePressed += ContinueGame;
        _menus.NewGamePressed += () => StartNewGame(NewSeed());
        _menus.ResumePressed += Resume;
        _menus.RestartPressed += () => StartNewGame(NewSeed());
        _menus.MainMenuPressed += GoToTitle;
        _menus.QuitPressed += () => GetTree().Quit();
        _menus.SettingsChanged += ApplySettings;

        ApplySettings();
        _audio.StartAmbience();
        RefreshBest();

        if (_autoplay && !_titleCapture)
            StartNewGame(20240611UL);
        else
            GoToTitle();

        if (_selfTest)
            RunSelfTest();
    }

    public override void _Input(InputEvent @event)
    {
        if (@event is InputEventMouse mouse)
            _mousePos = mouse.Position;
    }

    private Vector2 _mousePos;

    // Debug capture: periodic screenshots and FPS logging, used for automated visual checks.
    private string? _captureDir;
    private int _captureEvery;
    private bool _titleCapture;
    private int _frame;
    private float _perfTimer;

    private void UpdateCapture(float dt)
    {
        _frame++;
        _perfTimer += dt;
        if (_perfTimer >= 2f)
        {
            _perfTimer = 0f;
            GD.Print($"[perf] fps={Engine.GetFramesPerSecond():0} tiles={_session?.State.Board.Count} draw={Performance.GetMonitor(Performance.Monitor.RenderTotalDrawCallsInFrame)} prims={Performance.GetMonitor(Performance.Monitor.RenderTotalPrimitivesInFrame)}");
        }

        if (_captureDir is null || _frame % _captureEvery != 0)
            return;
        var image = GetViewport().GetTexture().GetImage();
        System.IO.Directory.CreateDirectory(_captureDir);
        image.SavePng(System.IO.Path.Combine(_captureDir, $"cap_{_frame:00000}.png"));
    }

    private static ulong NewSeed() => (ulong)DateTime.UtcNow.Ticks ^ 0x9E3779B97F4A7C15UL;

    // ================================================================== flow

    private void GoToTitle()
    {
        if (_mode == Mode.Playing || _mode == Mode.Paused)
            AutoSave();

        _mode = Mode.Title;
        _attract = true;
        _gameOverDelay = -1f;
        BeginSession(GameSession.StartNew(0xA77AC7UL ^ (ulong)DateTime.UtcNow.Minute, _sink), swoop: false);
        _hud.Visible = false;
        _rig.SetAutoOrbit(0.06f);
        _rig.ScreenShift = -0.75f;
        _board.SetCandidatesVisible(false);
        _menus.ShowTitle(_saves.TryLoad(ProjectSettings.GlobalizePath(SavePath), out var s, out _) && !s.IsGameOver, LoadLeaderboard());
    }

    private void StartNewGame(ulong seed)
    {
        if (!_autoplay)
            _saves.Delete(ProjectSettings.GlobalizePath(SavePath));
        _attract = false;
        _mode = Mode.Playing;
        _gameOverDelay = -1f;
        _menus.HideAll();
        _hud.Visible = true;
        _rig.SetAutoOrbit(0f);
        _rig.ScreenShift = 0f;
        _board.SetCandidatesVisible(_showCandidates);
        BeginSession(GameSession.StartNew(seed, _sink), swoop: true);
        _audio.Play(Sfx.Whoosh);
        _tutorialStage = _settings.TutorialDone || _autoplay || _selfTest ? 99 : 0;
        _tutorialMark = 0;
        UpdateTutorial();
    }

    private void ContinueGame()
    {
        if (!_saves.TryLoad(ProjectSettings.GlobalizePath(SavePath), out var state, out var error))
        {
            _hud.Toast($"Couldn't load your island: {error}");
            StartNewGame(NewSeed());
            return;
        }

        _attract = false;
        _mode = Mode.Playing;
        _menus.HideAll();
        _hud.Visible = true;
        _rig.SetAutoOrbit(0f);
        _rig.ScreenShift = 0f;
        _board.SetCandidatesVisible(_showCandidates);
        BeginSession(new GameSession(state, _sink), swoop: true);
        _tutorialStage = 99;
        _hud.ShowTip(null);
    }

    private void BeginSession(GameSession session, bool swoop)
    {
        _session = session;
        _sink.Clear();
        _scoreQueue.Clear();
        _hintSlot = null;
        _botPlan = null;
        _rotation = 0;
        _rotationTurns = 0;
        _handYaw = 0f;
        _board.Clear();
        _pendingFeedback.Clear();
        _shownScore = session.State.Score;

        // Cascade the island in from the centre outwards.
        var origin = HexGrid.Decode(HexGrid.OriginSlotIndex);
        foreach (var (slot, placed) in _session.State.Board.OrderBy(kv => HexGrid.Decode(kv.Key).DistanceTo(origin)))
        {
            var distance = HexGrid.Decode(slot).DistanceTo(origin);
            _board.AddTileImmediate(slot, placed, pop: true, delay: distance * 0.08f);
        }

        var state = _session.State;
        _hud.SetScore(state.Score, instant: true);
        _hud.ResetMilestoneBar();
        _hud.SetStreak(state.Streak, state.StreakMultiplier);
        RefreshHud();
        SetHand(state.CurrentTile, immediate: true);
        RefreshCandidates();
        _rig.Reframe(IslandCentre(), IslandRadius(), instant: true);
        if (swoop)
            _rig.IntroSwoop();
    }

    private void Pause()
    {
        if (_mode != Mode.Playing)
            return;
        _mode = Mode.Paused;
        _menus.ShowPause();
        _hud.SetHoverInfo(null, "", Colors.White);
        _audio.Play(Sfx.UiClick);
    }

    private void Resume()
    {
        if (_mode != Mode.Paused)
            return;
        _mode = Mode.Playing;
        _menus.HideAll();
    }

    private void ApplySettings()
    {
        Audio.SetBusLinear("Master", _settings.MasterVolume);
        Audio.SetBusLinear("Music", _settings.MusicVolume);
        Audio.SetBusLinear("Sfx", _settings.SfxVolume);
        Audio.SetBusLinear("Ambient", _settings.AmbientVolume);
        _world.ApplyQuality(_settings.Shadows, _settings.AmbientOcclusion, _settings.Bloom);
        _rig.DepthOfField = _settings.DepthOfField;
        _fx.Intensity = _settings.Particles ? 1f : 0f;
        var wantFull = _settings.Fullscreen;
        var isFull = DisplayServer.WindowGetMode() is DisplayServer.WindowMode.Fullscreen or DisplayServer.WindowMode.ExclusiveFullscreen;
        if (wantFull != isFull)
            DisplayServer.WindowSetMode(wantFull ? DisplayServer.WindowMode.Fullscreen : DisplayServer.WindowMode.Windowed);
    }

    // ================================================================== frame

    public override void _Process(double delta)
    {
        var dt = (float)delta;
        _time += delta;
        if (_autoplay || _captureDir is not null)
            UpdateCapture(dt);

        if (_mode == Mode.Playing && !_attract)
        {
            var pan = Vector2.Zero;
            if (Input.IsKeyPressed(Key.A) || Input.IsKeyPressed(Key.Left)) pan.X -= 1;
            if (Input.IsKeyPressed(Key.D) || Input.IsKeyPressed(Key.Right)) pan.X += 1;
            if (Input.IsKeyPressed(Key.W) || Input.IsKeyPressed(Key.Up)) pan.Y -= 1;
            if (Input.IsKeyPressed(Key.S) || Input.IsKeyPressed(Key.Down)) pan.Y += 1;
            _rig.PanWorld(pan.Normalized(), dt);
        }

        if (_attract || _autoplay)
            RunBot(dt);
        else
            UpdateHover();

        UpdateHand(dt);
        UpdateScoreQueue();
        UpdateQuestPins();

        if (_hintTimer > 0f)
        {
            _hintTimer -= dt;
            if (_hintTimer <= 0f)
            {
                _hintSlot = null;
                RefreshCandidates();
            }
        }

        if (_gameOverDelay > 0f)
        {
            _gameOverDelay -= dt;
            if (_gameOverDelay <= 0f)
                FinishRun();
        }

        if (_mode == Mode.Playing && !_attract && !_autoplay)
        {
            _idleHintTimer += dt;
            if (_idleHintTimer > 25f && _tutorialStage >= 99)
            {
                _idleHintTimer = 0f;
                _hud.Toast("Stuck? Press H for a hint.");
            }
        }

        _world.BirdCenter = IslandCentre();
        if (_session is not null)
        {
            var st = _session.State;
            _world.DaylightTarget = _attract ? 0.3f : 0.15f + (0.85f * st.Turn / Mathf.Max(1f, st.Turn + st.TilesLeft + 6f));
        }
        _rig.AutoFocus = IslandCentre();
    }

    // ================================================================== hand tile

    private void SetHand(TileSpec? tile, bool immediate)
    {
        _hand?.Free();
        _hand = null;
        _handTile = tile;
        if (tile is null)
            return;

        _hand = _board.CreateTileVisual(tile.Value, 0, Vector3.Zero, 4321);
        _hand.SetGrow(1f);
        _handRoot.AddChild(_hand.Root);
        _rotation = 0;
        _rotationTurns = 0;
        _handYaw = 0f;
        _handScale.Snap(immediate ? 1f : 0f);
        _handScale.Target = 1f;
        if (!immediate)
        {
            _handScale.Velocity = 2f;
            _hand.Root.Visible = false;
            _handSpawnDelay = 0.22f;
        }

        var start = _hoverSlot is int s ? HexLayout.SlotToWorld(s) + new Vector3(0, HoverHeight, 0) : _handPos.Value;
        if (immediate || _handPos.Value == Vector3.Zero)
            _handPos.Snap(start == Vector3.Zero ? IslandCentre() + new Vector3(2f, 1f, 2f) : start);
    }

    private void UpdateHover()
    {
        if (_mode != Mode.Playing || _session.State.IsGameOver)
        {
            SetHover(null);
            return;
        }

        var mouse = _mousePos;
        var overUi = GetViewport().GuiGetHoveredControl() is not null;
        if (overUi || _dragging)
        {
            if (overUi)
                SetHover(null);
            return;
        }

        var camera = _rig.Camera;
        var origin = camera.ProjectRayOrigin(mouse);
        var dir = camera.ProjectRayNormal(mouse);
        if (Mathf.Abs(dir.Y) < 0.0001f)
            return;
        var t = (0.05f - origin.Y) / dir.Y;
        if (t <= 0)
            return;
        var hit = origin + (dir * t);
        _cursorWorld = hit;
        var slot = HexLayout.WorldToSlot(hit);
        SetHover(_session.State.Board.ContainsKey(slot) || !_session.State.CandidateSlots().Contains(slot) ? null : slot);
    }

    private Vector3 _cursorWorld;

    private void SetHover(int? slot)
    {
        _hoverSlot = slot;
        if (slot != _lastHoverSlot)
        {
            _lastHoverSlot = slot;
            if (slot is not null && _hand is not null)
            {
                _audio.Play(Sfx.UiHover, 0.8f, -14f);
                _handHop.Velocity += 1.4f;
            }
        }

        RefreshHoverEval();
    }

    private void RefreshHoverEval()
    {
        _hoverEval = _hoverSlot is int s && _session.State.CurrentTile is not null
            ? GameEngine.Evaluate(_session.State, s, _rotation)
            : null;
    }

    private void UpdateHand(float dt)
    {
        if (_hand is null)
        {
            _board.HideEdgeMarkers();
            _hud.SetHoverInfo(null, "", Colors.White);
            return;
        }

        if (_handSpawnDelay > 0f)
        {
            _handSpawnDelay -= dt;
            if (_handSpawnDelay <= 0f)
            {
                _hand.Root.Visible = !_handHidden;
                _audio.Play(Sfx.UiHover, 1.3f, -8f);
            }
        }

        Vector3 target;
        var scaleTarget = 1f;
        if (_hoverSlot is int slot)
        {
            target = HexLayout.SlotToWorld(slot) + new Vector3(0, HoverHeight + (Mathf.Sin((float)_time * 2.6f) * 0.035f), 0);
        }
        else if (_mode == Mode.Playing && !_attract && _cursorWorld != Vector3.Zero)
        {
            target = _cursorWorld + new Vector3(0, 0.9f, 0);
            scaleTarget = 0.82f;
        }
        else
        {
            target = _handPos.Target;
        }

        _handPos.Target = target;
        _handPos.Step(dt);
        _handScale.Target = scaleTarget;
        _handScale.Step(dt);
        _handHop.Step(dt);

        var desiredYaw = _rotationTurns * (Mathf.Pi / 3f);
        _handYaw = Mathf.Lerp(_handYaw, desiredYaw, Ease.Damp(16f, dt));
        var vel = _handPos.Velocity;
        _hand.Root.Position = _handPos.Value + new Vector3(0, _handHop.Value * 0.12f, 0);
        _hand.Root.Rotation = new Vector3(Mathf.Clamp(vel.Z * 0.05f, -0.35f, 0.35f), 0, Mathf.Clamp(-vel.X * 0.05f, -0.35f, 0.35f));
        _hand.Body.Rotation = new Vector3(0, _handYaw, 0);
        _hand.Root.Scale = Vector3.One * Mathf.Max(0.01f, _handScale.Value);

        if (_hoverEval is { IsValid: true } eval && _hoverSlot is not null && _settings.EdgeHints && !_handHidden && !_attract)
        {
            _board.ShowEdgeMarkers(_hand.Root.Position + new Vector3(0, 0.12f, 0), eval);
            var screen = _rig.Camera.UnprojectPosition(_hand.Root.Position + new Vector3(0, 0.45f, 0));
            var (text, color) = DescribeEval(eval);
            _hud.SetHoverInfo(_mode == Mode.Playing && !_attract ? screen : null, text, color);
        }
        else
        {
            _board.HideEdgeMarkers();
            _hud.SetHoverInfo(null, "", Colors.White);
        }
    }

    private static (string Text, Color Color) DescribeEval(PlacementEvaluation eval)
    {
        var text = eval.TotalPoints > 0 ? $"+{eval.TotalPoints}" : "+0";
        if (eval.Quality == PlacementQuality.Flawless)
            return ($"{text}  ✦ FLAWLESS", Palette.Gold);
        if (eval.Quality == PlacementQuality.Perfect)
            return ($"{text}  ★ PERFECT", Palette.Gold);
        if (eval.BreaksStreak)
            return ($"{text}  · {eval.MismatchedEdges.Count} mismatch", new Color("ffb4a8"));
        return (text, Colors.White);
    }

    private void Rotate(int direction)
    {
        if (_hand is null || _mode != Mode.Playing)
            return;
        _rotation = GameEngine.NormalizeRotation(_rotation + direction);
        _rotationTurns += direction;
        _handHop.Velocity += 2.2f;
        _audio.Play(Sfx.Rotate, direction > 0 ? 1.05f : 0.95f);
        _hud.SetHandRotation(_rotation);
        RefreshHoverEval();
    }

    // ================================================================== placement

    private void TryPlace()
    {
        if (_mode != Mode.Playing || _hand is null || _hoverSlot is not int slot)
            return;
        Place(slot, _rotation);
    }

    private void Place(int slot, int rotation)
    {
        var eval = GameEngine.Evaluate(_session.State, slot, rotation);
        if (!eval.IsValid)
        {
            _audio.Play(Sfx.Invalid);
            _handPos.Velocity += new Vector3(4f, 0, 0);
            _hud.Toast(GameEngine.RejectionMessage(eval.RejectionReason!.Value), 1.6f);
            return;
        }

        var before = _session.State;
        var handPosition = _hand?.Root.Position ?? HexLayout.SlotToWorld(slot) + new Vector3(0, HoverHeight, 0);
        var tilt = _hand?.Root.Rotation ?? Vector3.Zero;
        var events = _session.Place(slot, rotation);
        _sink.Clear();
        var after = _session.State;
        _idleHintTimer = 0f;

        _hand?.Free();
        _hand = null;
        _hintSlot = null;
        _hintTimer = 0f;
        _hoverEval = null;

        var placed = after.Board[slot];
        var drop = _board.DropTile(slot, placed, handPosition.Y, tilt);
        _pendingFeedback[drop] = new PendingFeedback(before, after, events);
        _handPos.Snap(handPosition);

        SetHand(after.CurrentTile, immediate: false);
        RefreshCandidates();
        RefreshHud();

        if (!_attract)
            AutoSave();

        if (after.IsGameOver)
        {
            _gameOverDelay = 2.2f;
            if (!_attract)
                _hud.Toast("That was your last tile!", 2f);
        }

        if (!_attract)
            AdvanceTutorial(events);
    }

    private sealed record PendingFeedback(GameState Before, GameState After, IReadOnlyList<IDomainEvent> Events);

    private void OnTileLanded(int slot, TileVisual tile)
    {
        var world = HexLayout.SlotToWorld(slot);
        var waterHeavy = tile.Tile.ToArray().Count(t => t == Terrain.Water) >= 3;
        _audio.Play(waterHeavy ? Sfx.PlaceWater : Sfx.Place, 1f, _attract ? -10f : 0f);
        _fx.Burst(waterHeavy ? FxKind.Splash : FxKind.Dust, world + new Vector3(0, 0.05f, 0), 0.9f);
        if (tile.Tile.ToArray().Count(t => t == Terrain.Forest) >= 3)
            _fx.Burst(FxKind.Leaves, world + new Vector3(0, 0.25f, 0), 0.7f);
        _board.Ripple(slot, 1.4f);
        if (_settings.ScreenShake && !_attract)
            _rig.Shake(0.12f);

        if (_pendingFeedback.Remove(tile, out var feedback))
            PlayFeedback(slot, feedback);
    }

    /// <summary>Turns domain events into sound, particles, popups and banners, staggered so each beat reads.</summary>
    private void PlayFeedback(int slot, PendingFeedback feedback)
    {
        var world = HexLayout.SlotToWorld(slot);
        var screen = _rig.Camera.UnprojectPosition(world + new Vector3(0, 0.4f, 0));
        var quiet = _attract;
        var running = feedback.Before.Score;
        var delay = 0f;

        foreach (var ev in feedback.Events)
        {
            switch (ev)
            {
                case TilePlacedEvent placed:
                {
                    var e = placed.Evaluation;
                    for (var i = 0; i < e.MatchedEdges.Count; i++)
                    {
                        var edge = e.MatchedEdges[i];
                        var note = i + Math.Min(feedback.Before.Streak, 4);
                        var edgeWorld = world + (HexLayout.EdgeDirection(edge) * HexLayout.InRadius * 0.9f) + new Vector3(0, 0.15f, 0);
                        After(0.06f + (i * 0.075f), () =>
                        {
                            if (!quiet)
                                _audio.PlayNote(note);
                            _fx.Burst(FxKind.Sparkle, edgeWorld, 0.22f);
                        });
                    }

                    running += e.TotalPoints;
                    if (quiet)
                    {
                        _scoreQueue.Add((running, _time));
                        break;
                    }

                    if (e.TotalPoints > 0)
                    {
                        var color = e.Quality != PlacementQuality.Normal ? Palette.Gold : Colors.White;
                        _hud.ShowPopup(screen, $"+{e.TotalPoints}", color, e.Quality != PlacementQuality.Normal ? 40 : 32, delay: 0.05f);
                        var target = running;
                        _hud.FlyPoints(screen + new Vector2(0, -30), $"+{e.TotalPoints}", Palette.Accent, 0.45f, () =>
                        {
                            _scoreQueue.Add((target, _time));
                            _audio.Play(Sfx.UiHover, 1.6f, -6f);
                        });
                    }
                    else
                    {
                        _scoreQueue.Add((running, _time));
                    }

                    if (e.Quality == PlacementQuality.Flawless)
                    {
                        After(0.35f, () =>
                        {
                            _audio.Play(Sfx.Flawless);
                            _hud.Banner("Flawless!", "All six edges match", Palette.Gold, 1.2f);
                            _fx.Burst(FxKind.Sparkle, world + new Vector3(0, 0.3f, 0), 2f);
                            _board.Celebrate(HexGrid.GetNeighborSlots(slot).Append(slot), slot, Palette.Gold);
                            if (_settings.ScreenShake)
                                _rig.Shake(0.35f);
                        });
                        delay = 0.9f;
                    }
                    else if (e.Quality == PlacementQuality.Perfect)
                    {
                        After(0.3f, () =>
                        {
                            _audio.Play(Sfx.Perfect);
                            _hud.Banner("Perfect!", "+1 tile", Palette.Gold, 0.7f);
                            _fx.Burst(FxKind.Sparkle, world + new Vector3(0, 0.3f, 0), 1.2f);
                            _board.Flash(slot, Palette.Gold, 0.9f);
                            if (_settings.ScreenShake)
                                _rig.Shake(0.2f);
                        });
                        delay = 0.6f;
                    }

                    break;
                }

                case StreakChangedEvent streak:
                {
                    if (quiet)
                        break;
                    After(0.2f, () =>
                    {
                        _hud.SetStreak(streak.Current, streak.Multiplier);
                        if (streak.Current > streak.Previous)
                        {
                            _audio.Play(Sfx.StreakUp, 1f + (Math.Min(streak.Current, 10) * 0.05f), -4f);
                        }
                        else if (streak.Previous >= 3)
                        {
                            _audio.Play(Sfx.StreakLost, 1f, -4f);
                            _hud.Toast($"Streak of {streak.Previous} ended", 1.8f);
                        }
                    });
                    break;
                }

                case TilesAwardedEvent awarded when !quiet:
                {
                    var d = delay + 0.3f;
                    After(d, () => _hud.FlyTiles(screen, awarded.Amount, 0f));
                    break;
                }

                case QuestStartedEvent started when !quiet:
                {
                    After(0.5f, () =>
                    {
                        _audio.Play(Sfx.QuestNew);
                        var noun = Terrains.Info(started.Quest.Terrain).QuestNoun;
                        var kind = started.Quest.Kind == QuestKind.Exactly ? "exactly" : "to";
                        _hud.Toast($"New quest: grow this {noun} {kind} {started.Quest.Target}", 2.6f);
                    });
                    break;
                }

                case QuestCompletedEvent done:
                {
                    running += done.Quest.RewardPoints;
                    var target = running;
                    var questDelay = delay + 0.55f;
                    delay += 0.8f;
                    var regionSlots = RegionSlots(feedback.After, done.Quest.Anchor);
                    var anchorWorld = HexLayout.SlotToWorld(done.Quest.Anchor.Slot) + new Vector3(0, 0.5f, 0);
                    if (quiet)
                    {
                        _scoreQueue.Add((target, _time));
                        After(questDelay, () => _board.Celebrate(regionSlots, done.Quest.Anchor.Slot, Palette.Gold));
                        break;
                    }

                    After(questDelay, () =>
                    {
                        var anchorScreen = _rig.Camera.UnprojectPosition(anchorWorld);
                        _audio.Play(Sfx.QuestDone);
                        _hud.Banner("Quest complete!", $"+{done.Quest.RewardPoints} points  ·  +{done.Quest.RewardTiles} tiles", Palette.TerrainSwatch(done.Quest.Terrain).Lightened(0.25f), 1.3f);
                        _board.Celebrate(regionSlots, done.Quest.Anchor.Slot, Palette.Gold);
                        _fx.Burst(FxKind.Confetti, anchorWorld, 1.2f);
                        _hud.FlyPoints(anchorScreen, $"+{done.Quest.RewardPoints}", Palette.TerrainSwatch(done.Quest.Terrain), 0.3f, () => _scoreQueue.Add((target, _time)));
                        _hud.FlyTiles(anchorScreen, done.Quest.RewardTiles, 0.4f);
                        if (_settings.ScreenShake)
                            _rig.Shake(0.3f);
                    });
                    break;
                }

                case QuestFailedEvent failed when !quiet:
                {
                    After(delay + 0.5f, () =>
                    {
                        _audio.Play(Sfx.QuestFail);
                        var why = failed.Reason == QuestFailReason.Overshot ? "went past the exact size" : "got closed in";
                        _hud.Toast($"Quest failed: the {Terrains.Info(failed.Quest.Terrain).QuestNoun} {why}", 2.6f);
                    });
                    break;
                }

                case MilestoneReachedEvent milestone when !quiet:
                {
                    var d = delay + 0.9f;
                    delay += 1.0f;
                    After(d, () =>
                    {
                        _audio.Play(Sfx.Milestone);
                        var subtitle = milestone.UnlockedTerrain is Terrain t
                            ? $"{Terrains.DisplayName(t)} unlocked  ·  +{milestone.TilesAwarded} tiles"
                            : $"+{milestone.TilesAwarded} tiles";
                        _hud.Banner($"Milestone {milestone.Index + 1}!", subtitle, Palette.Gold, 1.5f);
                        _fx.Burst(FxKind.Confetti, IslandCentre() + new Vector3(0, 1.2f, 0), 1.6f);
                        _hud.FlyTiles(_hud.ScoreAnchor + new Vector2(0, 60), milestone.TilesAwarded, 0.2f);
                        if (_settings.ScreenShake)
                            _rig.Shake(0.4f);
                    });
                    break;
                }
            }
        }

        _scoreQueue.Add((feedback.After.Score, _time + 3.0));
    }

    private static List<int> RegionSlots(GameState state, SectorRef anchor)
    {
        var region = RegionMap.Build(state.Board).RegionAt(anchor);
        return region is null ? [anchor.Slot] : region.Sectors.Select(s => s.Slot).Distinct().ToList();
    }

    private void After(float seconds, Action action)
    {
        if (seconds <= 0f)
        {
            action();
            return;
        }

        GetTree().CreateTimer(seconds).Timeout += action;
    }

    private void UpdateScoreQueue()
    {
        // Each feedback beat pushes the visible score up; a final catch-up guarantees the counter never lags behind.
        for (var i = _scoreQueue.Count - 1; i >= 0; i--)
        {
            var (score, due) = _scoreQueue[i];
            if (_time < due)
                continue;
            _scoreQueue.RemoveAt(i);
            if (score > _shownScore)
            {
                _shownScore = score;
                _hud.SetScore(score);
                _hud.BumpScore();
                _hud.SetMilestone(score, MilestoneFloor(score), MilestoneCeiling(score), NextUnlockFor(score), GameRules.MilestoneTileReward);
            }
        }
    }

    private long _shownScore;

    private static long MilestoneFloor(long score)
    {
        long previous = 0;
        for (var i = 0; i < 200; i++)
        {
            var t = GameRules.MilestoneThreshold(i);
            if (t > score)
                return previous;
            previous = t;
        }

        return previous;
    }

    private static long MilestoneCeiling(long score)
    {
        for (var i = 0; i < 200; i++)
        {
            var t = GameRules.MilestoneThreshold(i);
            if (t > score)
                return t;
        }

        return score + 1;
    }

    private static Terrain? NextUnlockFor(long score)
    {
        for (var i = 0; i < 200; i++)
        {
            if (GameRules.MilestoneThreshold(i) > score)
                return GameRules.MilestoneUnlock(i);
        }

        return null;
    }

    // ================================================================== undo / hint

    private void Undo()
    {
        if (_mode != Mode.Playing || _attract)
            return;
        if (!_session.TryUndo(out var slot))
        {
            _audio.Play(Sfx.Invalid);
            _hud.Toast(_session.State.UndosRemaining <= 0 ? "No undos left on this island." : "Nothing to undo.", 1.6f);
            return;
        }

        _audio.Play(Sfx.Undo);
        if (_board.Tiles.TryGetValue(slot, out var tile))
        {
            var root = tile.Root;
            _board.RemoveTileDeferred(slot);
            var tween = root.CreateTween().SetParallel();
            tween.TweenProperty(root, "position:y", root.Position.Y + 0.8f, 0.25).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
            tween.TweenProperty(root, "scale", Vector3.One * 0.01f, 0.25).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.In);
            tween.Chain().TweenCallback(Callable.From(root.QueueFree));
        }

        var state = _session.State;
        _gameOverDelay = -1f;
        _scoreQueue.Clear();
        _shownScore = state.Score;
        _hud.SetScore(state.Score);
        _hud.SetStreak(state.Streak, state.StreakMultiplier);
        _handPos.Snap(HexLayout.SlotToWorld(slot) + new Vector3(0, HoverHeight + 0.6f, 0));
        SetHand(state.CurrentTile, immediate: false);
        _handSpawnDelay = 0.1f;
        RefreshCandidates();
        RefreshHud();
        AutoSave();
    }

    private void ShowHint()
    {
        if (_mode != Mode.Playing || _attract || _hand is null)
            return;
        var best = GameEngine.FindBestPlacement(_session.State);
        if (best is null)
            return;
        _hintSlot = best.Slot;
        _hintTimer = 4f;
        while (_rotation != best.Rotation)
            Rotate(+1);
        RefreshCandidates();
        _audio.Play(Sfx.QuestNew, 1.2f);
        _hud.Toast("Try the golden spot. The tile is already rotated for you.", 2.4f);
        _idleHintTimer = 0f;
    }

    // ================================================================== bot

    private void RunBot(float dt)
    {
        if (_session.State.IsGameOver)
        {
            if (_attract && _gameOverDelay <= 0f)
            {
                _gameOverDelay = -1f;
                BeginSession(GameSession.StartNew(NewSeed(), _sink), swoop: false);
            }

            return;
        }

        if (_mode == Mode.Paused || _menus.IsGameOverOpen || _hand is null)
            return;

        _botTimer -= dt;
        if (_botPlan is null)
        {
            if (_botTimer > 0f)
                return;
            _botPlan = GameEngine.FindBestPlacement(_session.State);
            _botTimer = _autoplay ? 0.55f : 1.1f;
            if (_botPlan is null)
                return;
            _hoverSlot = _botPlan.Slot;
            _lastHoverSlot = _hoverSlot;
            return;
        }

        if (_rotation != _botPlan.Rotation && _botTimer < (_autoplay ? 0.4f : 0.8f))
        {
            _rotation = GameEngine.NormalizeRotation(_rotation + 1);
            _rotationTurns++;
            _handHop.Velocity += 2f;
            _botTimer += 0.12f;
        }

        RefreshHoverEval();
        if (_botTimer <= 0f && _rotation == _botPlan.Rotation)
        {
            var plan = _botPlan;
            _botPlan = null;
            _botTimer = _autoplay ? 0.35f : 0.9f;
            Place(plan.Slot, plan.Rotation);
            _hoverSlot = null;
        }
    }

    // ================================================================== end of run

    private void FinishRun()
    {
        if (_attract)
            return;

        _mode = Mode.GameOver;
        var state = _session.State;
        var newBest = state.Score > _best;
        var rank = 0;
        try
        {
            if (!_autoplay)
                rank = _scores.RecordRun(ProjectSettings.GlobalizePath(ScoresPath),
                new RunRecord(state.Score, state.Stats.TilesPlaced, state.Stats.QuestsCompleted, state.Stats.BestStreak, DateTime.UtcNow));
        }
        catch (Exception ex)
        {
            GD.PushWarning($"Could not record run: {ex.Message}");
        }

        if (!_autoplay)
            _saves.Delete(ProjectSettings.GlobalizePath(SavePath));
        RefreshBest();
        _audio.Play(Sfx.GameOver);
        _rig.SetAutoOrbit(0.06f);
        _rig.Reframe(IslandCentre(), IslandRadius());
        _rig.ScreenShift = 0.9f;
        _hud.Visible = false;
        _board.SetCandidatesVisible(false);
        _hud.SetHoverInfo(null, "", Colors.White);
        _hud.ShowTip(null);
        _menus.ShowGameOver(state.Score, state.Stats, newBest, rank);
        if (newBest)
            After(1.7f, () => _fx.Burst(FxKind.Confetti, IslandCentre() + new Vector3(0, 1.5f, 0), 2f));

        if (_autoplay)
            After(4f, () => StartNewGame(NewSeed()));
    }

    // ================================================================== HUD sync

    private void RefreshHud()
    {
        var state = _session.State;
        _hud.SetStack(state.CurrentTile, state.NextTile, state.TilesLeft, _rotation);
        _hud.SetUndo(state.UndosRemaining, _session.CanUndo);
        _hud.SetQuests(GameEngine.GetQuestStatuses(state));
        _hud.SetBest(Math.Max(_best, _shownScore));
        if (_shownScore > state.Score || _shownScore == 0)
            _shownScore = state.Score;
        _hud.SetMilestone(_shownScore, MilestoneFloor(_shownScore), MilestoneCeiling(_shownScore), NextUnlockFor(_shownScore), GameRules.MilestoneTileReward);
        _hud.SetControlsHintVisible(state.Turn < 6);
    }

    private void RefreshCandidates()
    {
        var state = _session.State;
        _board.SetCandidates(state.IsGameOver ? Array.Empty<int>() : state.CandidateSlots(), _hintSlot);
        var radius = IslandRadius();
        _rig.FrameRadius = radius;
        _world.SetShelf(IslandCentre(), radius);
    }

    private void RefreshBest()
    {
        _best = _scores.TryLoadHighScore(ProjectSettings.GlobalizePath(ScoresPath), out var best, out _) ? best : 0;
        _hud?.SetBest(_best);
    }

    private IReadOnlyList<RunRecord> LoadLeaderboard() =>
        _scores.TryLoadBoard(ProjectSettings.GlobalizePath(ScoresPath), out var board, out _) ? board : [];

    private void AutoSave()
    {
        if (_attract || _autoplay || _session is null)
            return;
        try
        {
            if (_session.State.IsGameOver)
                _saves.Delete(ProjectSettings.GlobalizePath(SavePath));
            else
                _saves.Save(ProjectSettings.GlobalizePath(SavePath), _session.State);
        }
        catch (Exception ex)
        {
            GD.PushWarning($"Autosave failed: {ex.Message}");
        }
    }

    private void UpdateQuestPins()
    {
        if (_session is null || !_hud.Visible)
            return;
        if (!ReferenceEquals(_questCacheState, _session.State))
        {
            _questCacheState = _session.State;
            _questCache = GameEngine.GetQuestStatuses(_session.State);
        }

        var statuses = _questCache;
        var camera = _rig.Camera;
        var pins = new List<(int, Vector2?, QuestStatus)>();
        foreach (var status in statuses)
        {
            var world = RegionPinPosition(status);
            Vector2? screen = camera.IsPositionBehind(world) ? null : camera.UnprojectPosition(world);
            pins.Add((status.Quest.Id, screen, status));
        }

        _hud.UpdateQuestPins(pins);
    }

    private static Vector3 RegionPinPosition(QuestStatus status)
    {
        var sectors = status.Region?.Sectors ?? [status.Quest.Anchor];
        Vector3 SectorWorld(SectorRef s) => HexLayout.SlotToWorld(s.Slot) + (HexLayout.EdgeDirection(s.Edge) * 0.5f);
        var centroid = sectors.Aggregate(Vector3.Zero, (acc, s) => acc + SectorWorld(s)) / sectors.Count;
        var nearest = sectors.OrderBy(s => SectorWorld(s).DistanceSquaredTo(centroid)).First();
        return SectorWorld(nearest) + new Vector3(0, 0.55f, 0);
    }

    private Vector3 IslandCentre()
    {
        if (_session is null || _session.State.Board.Count == 0)
            return Vector3.Zero;
        var sum = Vector3.Zero;
        foreach (var slot in _session.State.Board.Keys)
            sum += HexLayout.SlotToWorld(slot);
        return sum / _session.State.Board.Count;
    }

    private float IslandRadius()
    {
        if (_session is null || _session.State.Board.Count == 0)
            return 1f;
        var centre = IslandCentre();
        return _session.State.Board.Keys.Max(s => HexLayout.SlotToWorld(s).DistanceTo(centre)) + 1f;
    }

    // ================================================================== tutorial

    private void UpdateTutorial()
    {
        string? tip = _tutorialStage switch
        {
            0 => "Hover over a glowing outline next to your island and click to lay the tile.",
            1 => "Rotate with Q / E or a right-click. Green edges match neighbouring terrain and score points; red ones don't.",
            2 => "Touch 3 or more tiles with every edge matching for a PERFECT: bonus points and a free tile!",
            3 => "A quest appeared! Grow that area to the target size for points and extra tiles. The pin on the map shows where.",
            4 => "Keep an eye on your stack (bottom-left). When it runs out, your island is complete.",
            _ => null
        };
        _hud.ShowTip(tip);
    }

    private void AdvanceTutorial(IReadOnlyList<IDomainEvent> events)
    {
        if (_tutorialStage >= 99)
            return;
        var turn = _session.State.Turn;
        var questAppeared = events.Any(e => e is QuestStartedEvent);
        var next = _tutorialStage switch
        {
            0 => 1,
            1 when turn >= 3 => 2,
            2 when questAppeared => 3,
            2 when turn >= 7 => 4,
            3 when turn >= _tutorialMark + 4 => 4,
            4 when turn >= _tutorialMark + 4 => 99,
            _ => _tutorialStage
        };
        if (_tutorialStage == 1 && questAppeared)
            next = 3;
        if (next != _tutorialStage)
        {
            _tutorialStage = next;
            _tutorialMark = turn;
            if (next >= 99)
            {
                _settings.TutorialDone = true;
                _settings.Save();
            }

            UpdateTutorial();
        }
    }

    // ================================================================== input

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is InputEventKey { Pressed: true, Echo: false } key)
        {
            HandleKey(key);
            return;
        }

        if (_mode != Mode.Playing || _attract)
            return;

        switch (@event)
        {
            case InputEventMouseButton mb:
                HandleMouseButton(mb);
                break;
            case InputEventMouseMotion motion:
                HandleMotion(motion);
                break;
        }
    }

    private void HandleKey(InputEventKey key)
    {
        if (key.Keycode == Key.Escape)
        {
            if (_menus.Back())
            {
                GetViewport().SetInputAsHandled();
                return;
            }

            if (_mode == Mode.Playing && !_attract)
                Pause();
            GetViewport().SetInputAsHandled();
            return;
        }

        if (key.Keycode == Key.F11)
        {
            _settings.Fullscreen = !_settings.Fullscreen;
            _settings.Save();
            ApplySettings();
            return;
        }

        if (_mode != Mode.Playing || _attract)
            return;

        switch (key.Keycode)
        {
            case Key.Q:
                Rotate(+1);
                break;
            case Key.E:
            case Key.R:
                Rotate(-1);
                break;
            case Key.F:
            case Key.Home:
                _rig.Reframe(IslandCentre(), IslandRadius());
                _audio.Play(Sfx.Whoosh, 1.3f, -6f);
                break;
            case Key.H:
            case Key.Space:
                ShowHint();
                break;
            case Key.Z when key.CtrlPressed:
            case Key.Backspace:
                Undo();
                break;
            case Key.T:
                _showCandidates = !_showCandidates;
                _board.SetCandidatesVisible(_showCandidates);
                break;
            default:
                return;
        }

        GetViewport().SetInputAsHandled();
    }

    private bool _showCandidates = true;

    private void HandleMouseButton(InputEventMouseButton mb)
    {
        switch (mb.ButtonIndex)
        {
            case MouseButton.WheelUp when mb.Pressed:
                if (mb.ShiftPressed)
                    Rotate(+1);
                else
                    _rig.Zoom(-1f);
                break;
            case MouseButton.WheelDown when mb.Pressed:
                if (mb.ShiftPressed)
                    Rotate(-1);
                else
                    _rig.Zoom(1f);
                break;
            case MouseButton.Left:
                if (mb.Pressed)
                {
                    _lmbDown = true;
                    _pressPos = mb.Position;
                    _dragging = false;
                }
                else
                {
                    if (_lmbDown && !_dragging)
                        TryPlace();
                    _lmbDown = false;
                    _dragging = false;
                }

                break;
            case MouseButton.Right:
                if (mb.Pressed)
                {
                    _rmbDown = true;
                    _pressPos = mb.Position;
                    _dragging = false;
                }
                else
                {
                    if (_rmbDown && !_dragging)
                        Rotate(-1);
                    _rmbDown = false;
                    _dragging = false;
                }

                break;
            case MouseButton.Middle:
                _mmbDown = mb.Pressed;
                break;
            default:
                return;
        }

        GetViewport().SetInputAsHandled();
    }

    private void HandleMotion(InputEventMouseMotion motion)
    {
        if ((_lmbDown || _rmbDown) && !_dragging && motion.Position.DistanceTo(_pressPos) > 7f)
            _dragging = true;

        if (_mmbDown || (_lmbDown && _dragging))
            _rig.Pan(motion.Relative);
        else if (_rmbDown && _dragging)
            _rig.Orbit(motion.Relative);
    }
}
