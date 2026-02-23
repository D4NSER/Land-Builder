using Godot;
using LandBuilder.Application;
using LandBuilder.Domain;
using LandBuilder.Infrastructure;
using LandBuilder.Infrastructure.Content;

namespace LandBuilder.Presentation;

public partial class MainController : Control
{
    private enum BuildMode
    {
        None = 0,
        Camp = 1,
        Quarry = 2,
        Sawmill = 3
    }

    private sealed record HotkeyBinding(Key Key, bool CtrlRequired, Action Action, string HelpText);

    private readonly InMemoryEventSink _eventSink = new();
    private readonly Queue<string> _recentNotifications = new();
    private readonly List<HotkeyBinding> _hotkeys = new();

    private SaveRepository _saveRepository = null!;
    private GameSession _session = null!;
    private DeterministicTickScheduler _scheduler = null!;

    private Label _hotkeysLabel = null!;
    private Label _coinsLabel = null!;
    private Label _statsLabel = null!;
    private Label _objectiveLabel = null!;
    private Label _objectiveProgressLabel = null!;
    private Label _completionLabel = null!;
    private Label _messageLabel = null!;
    private Label _notificationsLabel = null!;
    private Label _buildModeLabel = null!;
    private Label _tileStatesLabel = null!;
    private Label _tileActionHintLabel = null!;
    private Camera2D _camera = null!;

    private Button _expandTile1Button = null!;
    private Button _expandTile2Button = null!;
    private Button _tile0ActionButton = null!;
    private Button _tile2ActionButton = null!;

    private BuildMode _buildMode = BuildMode.None;

    private long _lastAutosaveMsec;
    private const int AutosaveCooldownMsec = 750;
    private const float CameraPanSpeed = 500f;
    private const float CameraZoomStep = 0.1f;

    private const string PrimarySavePath = "user://mvp2_save.json";
    private const string BackupSavePath = "user://mvp2_save.backup.json";
    private const string AutosavePath = "user://mvp2_autosave.json";
    private const string AutosaveBackupPath = "user://mvp2_autosave.backup.json";

    public override void _Ready()
    {
        var objectivePath = ProjectSettings.GlobalizePath("res://data/objectives/mvp2_objectives.json");
        var objectiveDefinitions = new ObjectiveDefinitionLoader().Load(objectivePath);

        _saveRepository = new SaveRepository(objectiveDefinitions);
        _session = new GameSession(GameState.CreateInitial(objectiveDefinitions), _eventSink);
        _scheduler = new DeterministicTickScheduler(_session, ticksPerSecond: 10);

        _hotkeysLabel = GetNode<Label>("VBox/HotkeysLabel");
        _coinsLabel = GetNode<Label>("VBox/CoinsLabel");
        _statsLabel = GetNode<Label>("VBox/StatsLabel");
        _objectiveLabel = GetNode<Label>("VBox/ObjectiveLabel");
        _objectiveProgressLabel = GetNode<Label>("VBox/ObjectiveProgressLabel");
        _completionLabel = GetNode<Label>("VBox/CompletionLabel");
        _messageLabel = GetNode<Label>("VBox/MessageLabel");
        _notificationsLabel = GetNode<Label>("VBox/NotificationsLabel");
        _buildModeLabel = GetNode<Label>("VBox/BuildModeLabel");
        _tileStatesLabel = GetNode<Label>("VBox/TileStatesLabel");
        _tileActionHintLabel = GetNode<Label>("VBox/TileActionHintLabel");
        _camera = GetNode<Camera2D>("GameCamera");

        _expandTile1Button = GetNode<Button>("VBox/Buttons/ExpandTile1");
        _expandTile2Button = GetNode<Button>("VBox/Buttons/ExpandTile2");
        _tile0ActionButton = GetNode<Button>("VBox/Buttons/Tile0Action");
        _tile2ActionButton = GetNode<Button>("VBox/Buttons/Tile2Action");

        BindButtons();
        ConfigureHotkeys();
        RenderProjection();
    }

    public override void _Process(double delta)
    {
        var ticks = _scheduler.Advance(delta);
        if (ticks > 0)
            RenderProjection();

        HandleCameraPan((float)delta);
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is not InputEventKey key || !key.Pressed || key.Echo)
            return;

        if (key.Keycode == Key.Escape)
        {
            CancelBuildMode();
            return;
        }

        if (key.Keycode == Key.Equal || key.Keycode == Key.KpAdd)
        {
            _camera.Zoom = (_camera.Zoom - new Vector2(CameraZoomStep, CameraZoomStep)).Max(new Vector2(0.5f, 0.5f));
            PushNotification($"Camera zoom: {_camera.Zoom.X:0.00}");
            return;
        }

        if (key.Keycode == Key.Minus || key.Keycode == Key.KpSubtract)
        {
            _camera.Zoom = (_camera.Zoom + new Vector2(CameraZoomStep, CameraZoomStep)).Min(new Vector2(2.0f, 2.0f));
            PushNotification($"Camera zoom: {_camera.Zoom.X:0.00}");
            return;
        }

        var binding = _hotkeys.FirstOrDefault(x => x.Key == key.Keycode && x.CtrlRequired == key.CtrlPressed);
        binding?.Action.Invoke();
    }

    private void ConfigureHotkeys()
    {
        _hotkeys.Clear();

        _hotkeys.Add(new HotkeyBinding(Key.Key1, false, () => IssueCommand(new ExpandTileCommand(1)), "1: Expand Tile 1"));
        _hotkeys.Add(new HotkeyBinding(Key.Key2, false, () => IssueCommand(new ExpandTileCommand(2)), "2: Expand Tile 2"));
        _hotkeys.Add(new HotkeyBinding(Key.C, false, () => EnterBuildMode(BuildMode.Camp), "C: Camp Build Mode"));
        _hotkeys.Add(new HotkeyBinding(Key.Q, false, () => EnterBuildMode(BuildMode.Quarry), "Q: Quarry Build Mode"));
        _hotkeys.Add(new HotkeyBinding(Key.B, false, () => EnterBuildMode(BuildMode.Sawmill), "B: Sawmill Build Mode"));
        _hotkeys.Add(new HotkeyBinding(Key.T, false, () => AttemptBuildOnTile(0), "T: Build on Tile 0"));
        _hotkeys.Add(new HotkeyBinding(Key.Y, false, () => AttemptBuildOnTile(2), "Y: Build on Tile 2"));
        _hotkeys.Add(new HotkeyBinding(Key.U, false, () => IssueCommand(new UpgradeBuildingCommand(1)), "U: Upgrade Building 1"));
        _hotkeys.Add(new HotkeyBinding(Key.S, true, SaveState, "Ctrl+S: Save"));
        _hotkeys.Add(new HotkeyBinding(Key.L, true, LoadState, "Ctrl+L: Load"));

        var duplicate = _hotkeys
            .GroupBy(x => (x.Key, x.CtrlRequired))
            .FirstOrDefault(g => g.Count() > 1);

        if (duplicate is not null)
            throw new InvalidOperationException($"Conflicting hotkey binding detected for {duplicate.Key.Key} (CtrlRequired={duplicate.Key.CtrlRequired}).");

        _hotkeysLabel.Text =
            "Hotkeys: " +
            string.Join(" | ", _hotkeys.Select(h => h.HelpText)) +
            " | Esc: Cancel Build Mode | WASD/Arrows: Pan | +/-: Zoom";
    }

    private void HandleCameraPan(float delta)
    {
        var direction = Vector2.Zero;

        if (Input.IsKeyPressed(Key.Left) || Input.IsKeyPressed(Key.A)) direction.X -= 1;
        if (Input.IsKeyPressed(Key.Right) || Input.IsKeyPressed(Key.D)) direction.X += 1;
        if (Input.IsKeyPressed(Key.Up) || Input.IsKeyPressed(Key.W)) direction.Y -= 1;
        if (Input.IsKeyPressed(Key.Down) || Input.IsKeyPressed(Key.S)) direction.Y += 1;

        if (direction == Vector2.Zero) return;

        _camera.Position += direction.Normalized() * CameraPanSpeed * delta;
    }

    private void BindButtons()
    {
        _expandTile1Button.Pressed += () => IssueCommand(new ExpandTileCommand(1));
        _expandTile2Button.Pressed += () => IssueCommand(new ExpandTileCommand(2));
        GetNode<Button>("VBox/Buttons/EnterCampBuildMode").Pressed += () => EnterBuildMode(BuildMode.Camp);
        GetNode<Button>("VBox/Buttons/EnterQuarryBuildMode").Pressed += () => EnterBuildMode(BuildMode.Quarry);
        GetNode<Button>("VBox/Buttons/EnterSawmillBuildMode").Pressed += () => EnterBuildMode(BuildMode.Sawmill);
        GetNode<Button>("VBox/Buttons/CancelBuildMode").Pressed += CancelBuildMode;
        _tile0ActionButton.Pressed += () => AttemptBuildOnTile(0);
        _tile2ActionButton.Pressed += () => AttemptBuildOnTile(2);
        GetNode<Button>("VBox/Buttons/UpgradeBuilding1").Pressed += () => IssueCommand(new UpgradeBuildingCommand(1));
        GetNode<Button>("VBox/Buttons/SaveButton").Pressed += SaveState;
        GetNode<Button>("VBox/Buttons/LoadButton").Pressed += LoadState;
    }

    private void EnterBuildMode(BuildMode mode)
    {
        _buildMode = mode;
        RenderProjection($"{mode} build mode active. Choose tile action button (T/Y).");
    }

    private void CancelBuildMode()
    {
        if (_buildMode == BuildMode.None)
        {
            RenderProjection("No build mode active.");
            return;
        }

        _buildMode = BuildMode.None;
        RenderProjection("Build mode cancelled.");
    }

    private void AttemptBuildOnTile(int tileId)
    {
        if (_buildMode == BuildMode.None)
        {
            RenderProjection("Enter build mode first (C or Q).");
            return;
        }

        var buildingType = _buildMode switch
        {
            BuildMode.Camp => "Camp",
            BuildMode.Quarry => "Quarry",
            BuildMode.Sawmill => "Sawmill",
            _ => "Camp"
        };
        var validation = DeterministicSimulator.ValidatePlacement(_session.State, buildingType, tileId);
        if (!validation.IsValid)
        {
            RenderProjection($"Build blocked ({validation.ReasonCode}): {validation.Message}");
            return;
        }

        IssueCommand(new PlaceBuildingCommand(buildingType, tileId));
    }

    private void IssueCommand(IGameCommand command)
    {
        _session.Dispatch(command);
        AutosaveOnKeyEvents();
        RenderProjection();
    }

    private void AutosaveOnKeyEvents()
    {
        if (!_eventSink.Events.Any(e => e is ObjectiveCompletedEvent or TileUnlockedEvent or BuildingPlacedEvent or BuildingUpgradedEvent))
            return;

        var now = Time.GetTicksMsec();
        if (now - _lastAutosaveMsec < AutosaveCooldownMsec)
            return;

        _saveRepository.Save(AutosavePath, _session.State, AutosaveBackupPath);
        _lastAutosaveMsec = now;
        PushNotification("Autosave complete");
    }

    private void SaveState()
    {
        _saveRepository.Save(PrimarySavePath, _session.State, BackupSavePath);
        RenderProjection($"Saved to {PrimarySavePath}");
    }

    private void LoadState()
    {
        var result = _saveRepository.LoadWithRecovery(
            PrimarySavePath,
            BackupSavePath,
            () => GameState.CreateInitial(_session.State.ObjectiveDefinitions));

        _session = new GameSession(result.State, _eventSink);
        _scheduler = new DeterministicTickScheduler(_session, ticksPerSecond: 10);
        _buildMode = BuildMode.None;

        RenderProjection(result.StatusMessage);
        PushNotification(result.StatusMessage);
    }

    private void RenderProjection(string? overrideMessage = null)
    {
        var projection = UiProjection.From(_session.State, _eventSink.Events);
        _coinsLabel.Text = $"Coins: {projection.Coins}";
        var contributionText = projection.BuildingContributions.Count == 0
            ? "none"
            : string.Join(", ", projection.BuildingContributions.Select(c => $"{c.BuildingTypeId}#{c.BuildingId}=+{c.ContributionPerTick}"));
        _statsLabel.Text = $"Buildings: {projection.BuildingsCount} | Production/tick: {projection.ProductionPerTick} | Contributions: {contributionText}";
        _objectiveLabel.Text = $"Active Objective: {projection.ActiveObjectiveId}";
        _objectiveProgressLabel.Text = $"Progress: {projection.ObjectiveProgress}";
        _completionLabel.Text = $"Last Completion: {projection.LastCompletionMessage}";
        _messageLabel.Text = overrideMessage ?? projection.LastEventMessage;

        _buildModeLabel.Text = $"Build Mode: {_buildMode}";
        _tileActionHintLabel.Text = _buildMode == BuildMode.None
            ? "Tile Actions: Enter build mode to place buildings."
            : $"Tile Actions: placing {_buildMode} (Esc to cancel).";

        var tileSummaries = projection.TileStates
            .Select(t => $"Tile {t.TileId}: {t.State} | Expand {t.ExpansionPreviewCost} ({(t.CanExpand ? "valid" : t.ExpansionReasonCode.ToString())})")
            .ToArray();
        _tileStatesLabel.Text = "Tile States:\n" + string.Join("\n", tileSummaries);

        UpdateTileButtons(projection);

        var messageForFeed = overrideMessage ?? projection.LastEventMessage;
        if (!string.IsNullOrWhiteSpace(messageForFeed) && messageForFeed != "Ready")
            PushNotification(messageForFeed);

        _eventSink.Clear();
    }

    private void UpdateTileButtons(UiProjection projection)
    {
        var tile1 = projection.TileStates.First(t => t.TileId == 1);
        var tile2 = projection.TileStates.First(t => t.TileId == 2);

        _expandTile1Button.Text = $"Expand Tile 1 ({tile1.ExpansionPreviewCost} coins)";
        _expandTile2Button.Text = $"Expand Tile 2 ({tile2.ExpansionPreviewCost} coins)";

        var mode = _buildMode switch
        {
            BuildMode.Camp => "Camp",
            BuildMode.Quarry => "Quarry",
            BuildMode.Sawmill => "Sawmill",
            _ => "Camp"
        };
        var tile0 = projection.TileStates.First(t => t.TileId == 0);
        var tile2Build = projection.TileStates.First(t => t.TileId == 2);

        if (_buildMode == BuildMode.None)
        {
            _tile0ActionButton.Text = "Tile 0 Action (build mode required)";
            _tile2ActionButton.Text = "Tile 2 Action (build mode required)";
            _tile0ActionButton.Disabled = true;
            _tile2ActionButton.Disabled = true;
            return;
        }

        var tile0Valid = _buildMode switch
        {
            BuildMode.Camp => tile0.CanPlaceCamp,
            BuildMode.Quarry => tile0.CanPlaceQuarry,
            BuildMode.Sawmill => tile0.CanPlaceSawmill,
            _ => false
        };
        var tile2Valid = _buildMode switch
        {
            BuildMode.Camp => tile2Build.CanPlaceCamp,
            BuildMode.Quarry => tile2Build.CanPlaceQuarry,
            BuildMode.Sawmill => tile2Build.CanPlaceSawmill,
            _ => false
        };
        var tile0Reason = _buildMode switch
        {
            BuildMode.Camp => tile0.CampReasonCode,
            BuildMode.Quarry => tile0.QuarryReasonCode,
            BuildMode.Sawmill => tile0.SawmillReasonCode,
            _ => ValidationReasonCode.UnknownCommand
        };
        var tile2Reason = _buildMode switch
        {
            BuildMode.Camp => tile2Build.CampReasonCode,
            BuildMode.Quarry => tile2Build.QuarryReasonCode,
            BuildMode.Sawmill => tile2Build.SawmillReasonCode,
            _ => ValidationReasonCode.UnknownCommand
        };

        _tile0ActionButton.Text = $"{(tile0Valid ? "[VALID]" : "[INVALID]")} {mode} on Tile 0 ({(tile0Valid ? "OK" : tile0Reason)})";
        _tile2ActionButton.Text = $"{(tile2Valid ? "[VALID]" : "[INVALID]")} {mode} on Tile 2 ({(tile2Valid ? "OK" : tile2Reason)})";
        _tile0ActionButton.Disabled = false;
        _tile2ActionButton.Disabled = false;
    }

    private void PushNotification(string text)
    {
        _recentNotifications.Enqueue(text);
        while (_recentNotifications.Count > 5)
            _recentNotifications.Dequeue();

        _notificationsLabel.Text = "Notifications:\n" + string.Join("\n", _recentNotifications);
    }
}
