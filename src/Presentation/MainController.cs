using Godot;
using LandBuilder.Application;
using LandBuilder.Domain;
using LandBuilder.Infrastructure;
using LandBuilder.Infrastructure.Content;

namespace LandBuilder.Presentation;

public partial class MainController : Control
{
    private readonly InMemoryEventSink _eventSink = new();
    private SaveRepository _saveRepository = null!;

    private GameSession _session = null!;
    private DeterministicTickScheduler _scheduler = null!;

    private Label _coinsLabel = null!;
    private Label _statsLabel = null!;
    private Label _objectiveLabel = null!;
    private Label _objectiveProgressLabel = null!;
    private Label _completionLabel = null!;
    private Label _messageLabel = null!;

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

        if (_eventSink.Events.Any(e => e is ObjectiveCompletedEvent or TileUnlockedEvent or BuildingPlacedEvent or BuildingUpgradedEvent))
        {
            _saveRepository.Save("user://mvp2_autosave.json", _session.State);
        }

        RenderProjection();
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
        _eventSink.Clear();
    }
}
