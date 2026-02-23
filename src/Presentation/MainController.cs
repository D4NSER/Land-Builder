using System.Linq;
using Godot;
using LandBuilder.Application;
using LandBuilder.Domain;
using LandBuilder.Infrastructure;

namespace LandBuilder.Presentation;

public partial class MainController : Control
{
    private readonly InMemoryEventSink _eventSink = new();
    private SaveRepository _saveRepository = null!;
    private GameSession _session = null!;

    private Label _coinsLabel = null!;
    private Label _currentTileLabel = null!;
    private Label _boardLabel = null!;
    private Label _messageLabel = null!;

    private const string SavePath = "user://land_builder_save.json";

    public override void _Ready()
    {
        _saveRepository = new SaveRepository();
        _session = new GameSession(GameState.CreateInitial(), _eventSink);

        _coinsLabel = GetNode<Label>("VBox/CoinsLabel");
        _currentTileLabel = GetNode<Label>("VBox/CurrentTileLabel");
        _boardLabel = GetNode<Label>("VBox/BoardLabel");
        _messageLabel = GetNode<Label>("VBox/MessageLabel");

        GetNode<Button>("VBox/Buttons/DrawTile").Pressed += () => Issue(new DrawTileCommand());
        GetNode<Button>("VBox/Buttons/Save").Pressed += Save;
        GetNode<Button>("VBox/Buttons/Load").Pressed += Load;

        for (var i = 0; i < 9; i++)
        {
            var idx = i;
            GetNode<Button>($"VBox/Buttons/Place{idx}").Pressed += () => Issue(new PlaceTileCommand(idx, 0));
        }

        Render();
    }

    private void Issue(IGameCommand command)
    {
        _session.IssueCommand(command);
        Render();
    }

    private void Save()
    {
        _saveRepository.Save(ProjectSettings.GlobalizePath(SavePath), _session.State);
        _messageLabel.Text = "Saved.";
    }

    private void Load()
    {
        _session = new GameSession(_saveRepository.Load(ProjectSettings.GlobalizePath(SavePath)), _eventSink);
        Render();
        _messageLabel.Text = "Loaded.";
    }

    private void Render()
    {
        var p = UiProjectionBuilder.Build(_session.State);
        _coinsLabel.Text = $"Coins: {p.Coins}";
        _currentTileLabel.Text = $"Current tile: {p.CurrentTile}";
        _boardLabel.Text = string.Join("\n", p.Slots.Select(s => $"Slot {s.SlotIndex}: {s.Occupant}"));
        _messageLabel.Text = p.LastMessage;
    }
}
