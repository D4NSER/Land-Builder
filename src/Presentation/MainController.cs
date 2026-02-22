using Godot;
using LandBuilder.Application;
using LandBuilder.Domain;
using LandBuilder.Infrastructure;
using LandBuilder.Infrastructure.Content;

namespace LandBuilder.Presentation;

public partial class MainController : Control
{
    private readonly InMemoryEventSink _eventSink = new();
    private readonly Queue<string> _recentNotifications = new();

    private SaveRepository _saveRepository = null!;
    private GameSession _session = null!;
    private DeterministicTickScheduler _scheduler = null!;

    private Label _coinsLabel = null!;
    private Label _statsLabel = null!;
    private Label _objectiveLabel = null!;
    private Label _objectiveProgressLabel = null!;
    private Label _completionLabel = null!;
    private Label _messageLabel = null!;
    private Label _notificationsLabel = null!;
    private Camera2D _camera = null!;

    private long _lastAutosaveMsec;
    private const int AutosaveCooldownMsec = 750;
    private const float CameraPanSpeed = 500f;
    private const float CameraZoomStep = 0.1f;

    public override void _Ready()
    {
        var objectivePath = ProjectSettings.GlobalizePath("res://data/objectives/mvp2_objectives.json");
        var objectiveDefinitions = new ObjectiveDefinitionLoader().Load(objectivePath);

        _saveRepository = new SaveRepository(objectiveDefinitions);
        _session = new GameSession(GameState.CreateInitial(objectiveDefinitions), _eventSink);
        _scheduler = new DeterministicTickScheduler(_session, ticksPerSecond: 10);

        _coinsLabel = GetNode<Label>("VBox/CoinsLabel");
        _statsLabel = GetNode<Label>("VBox/StatsLabel");
        _objectiveLabel = GetNode<Label>("VBox/ObjectiveLabel");
        _objectiveProgressLabel = GetNode<Label>("VBox/ObjectiveProgressLabel");
        _completionLabel = GetNode<Label>("VBox/CompletionLabel");
        _messageLabel = GetNode<Label>("VBox/MessageLabel");
        _notificationsLabel = GetNode<Label>("VBox/NotificationsLabel");
        _camera = GetNode<Camera2D>("GameCamera");

        BindButtons();
        RenderProjection();
    }

    public override void _Process(double delta)
    {
        var ticks = _scheduler.Advance(delta);
        if (ticks > 0)
        {
            RenderProjection();
        }

        HandleCameraPan((float)delta);
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is not InputEventKey key || !key.Pressed || key.Echo)
            return;

        if (key.Keycode == Key.Escape)
        {
            RenderProjection("Cancelled current action");
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

        switch (key.Keycode)
        {
            case Key.Key1:
                IssueCommand(new ExpandTileCommand(1));
                break;
            case Key.Key2:
                IssueCommand(new ExpandTileCommand(2));
                break;
            case Key.C:
                IssueCommand(new PlaceBuildingCommand("Camp", 0));
                break;
            case Key.Q:
                IssueCommand(new PlaceBuildingCommand("Quarry", 2));
                break;
            case Key.U:
                IssueCommand(new UpgradeBuildingCommand(1));
                break;
            case Key.S when key.CtrlPressed:
                SaveState();
                break;
            case Key.L when key.CtrlPressed:
                LoadState();
                break;
        }
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
        GetNode<Button>("VBox/Buttons/ExpandTile1").Pressed += () => IssueCommand(new ExpandTileCommand(1));
        GetNode<Button>("VBox/Buttons/ExpandTile2").Pressed += () => IssueCommand(new ExpandTileCommand(2));
        GetNode<Button>("VBox/Buttons/PlaceCampTile0").Pressed += () => IssueCommand(new PlaceBuildingCommand("Camp", 0));
        GetNode<Button>("VBox/Buttons/PlaceQuarryTile2").Pressed += () => IssueCommand(new PlaceBuildingCommand("Quarry", 2));
        GetNode<Button>("VBox/Buttons/UpgradeBuilding1").Pressed += () => IssueCommand(new UpgradeBuildingCommand(1));
        GetNode<Button>("VBox/Buttons/SaveButton").Pressed += SaveState;
        GetNode<Button>("VBox/Buttons/LoadButton").Pressed += LoadState;
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

        _saveRepository.Save("user://mvp2_autosave.json", _session.State);
        _lastAutosaveMsec = now;
        PushNotification("Autosave complete");
    }

    private void SaveState()
    {
        _saveRepository.Save("user://mvp2_save.json", _session.State);
        RenderProjection("Saved to user://mvp2_save.json");
    }

    private void LoadState()
    {
        var loaded = _saveRepository.Load("user://mvp2_save.json");
        _session = new GameSession(loaded, _eventSink);
        _scheduler = new DeterministicTickScheduler(_session, ticksPerSecond: 10);
        RenderProjection("Loaded from user://mvp2_save.json");
    }

    private void RenderProjection(string? overrideMessage = null)
    {
        var projection = UiProjection.From(_session.State, _eventSink.Events);
        _coinsLabel.Text = $"Coins: {projection.Coins}";
        _statsLabel.Text = $"Buildings: {projection.BuildingsCount} | Production/tick: {projection.ProductionPerTick}";
        _objectiveLabel.Text = $"Active Objective: {projection.ActiveObjectiveId}";
        _objectiveProgressLabel.Text = $"Progress: {projection.ObjectiveProgress}";
        _completionLabel.Text = $"Last Completion: {projection.LastCompletionMessage}";
        _messageLabel.Text = overrideMessage ?? projection.LastEventMessage;

        var messageForFeed = overrideMessage ?? projection.LastEventMessage;
        if (!string.IsNullOrWhiteSpace(messageForFeed) && messageForFeed != "Ready")
            PushNotification(messageForFeed);

        _eventSink.Clear();
    }

    private void PushNotification(string text)
    {
        _recentNotifications.Enqueue(text);
        while (_recentNotifications.Count > 5)
            _recentNotifications.Dequeue();

        _notificationsLabel.Text = "Notifications:\n" + string.Join("\n", _recentNotifications);
    }
}
