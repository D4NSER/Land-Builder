using Godot;
using LandBuilder.Application;
using LandBuilder.Domain;
using LandBuilder.Infrastructure;

namespace LandBuilder.Presentation;

public partial class MainController : Control
{
    private readonly InMemoryEventSink _eventSink = new();
    private readonly SaveRepository _saveRepository = new();

    private GameSession _session = null!;
    private DeterministicTickScheduler _scheduler = null!;

    private Label _coinsLabel = null!;
    private Label _statsLabel = null!;
    private Label _messageLabel = null!;

    public override void _Ready()
    {
        _session = new GameSession(GameState.CreateMvp0Default(), _eventSink);
        _scheduler = new DeterministicTickScheduler(_session, ticksPerSecond: 10);

        _coinsLabel = GetNode<Label>("VBox/CoinsLabel");
        _statsLabel = GetNode<Label>("VBox/StatsLabel");
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
        GetNode<Button>("VBox/Buttons/PlaceCampTile0").Pressed += () => IssueCommand(new PlaceBuildingCommand("Camp", 0));
        GetNode<Button>("VBox/Buttons/UpgradeBuilding1").Pressed += () => IssueCommand(new UpgradeBuildingCommand(1));
        GetNode<Button>("VBox/Buttons/SaveButton").Pressed += SaveState;
        GetNode<Button>("VBox/Buttons/LoadButton").Pressed += LoadState;
    }

    private void IssueCommand(IGameCommand command)
    {
        _session.Dispatch(command);
        RenderProjection();
    }

    private void SaveState()
    {
        _saveRepository.Save("user://mvp1_save.json", _session.State);
        RenderProjection("Saved to user://mvp1_save.json");
    }

    private void LoadState()
    {
        var loaded = _saveRepository.Load("user://mvp1_save.json");
        _session = new GameSession(loaded, _eventSink);
        _scheduler = new DeterministicTickScheduler(_session, ticksPerSecond: 10);
        RenderProjection("Loaded from user://mvp1_save.json");
    }

    private void RenderProjection(string? overrideMessage = null)
    {
        var projection = UiProjection.From(_session.State, _eventSink.Events);
        _coinsLabel.Text = $"Coins: {projection.Coins}";
        _statsLabel.Text = $"Buildings: {projection.BuildingsCount} | Production/tick: {projection.ProductionPerTick}";
        _messageLabel.Text = overrideMessage ?? projection.LastEventMessage;
        _eventSink.Clear();
    }
}
