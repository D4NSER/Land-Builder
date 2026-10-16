using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;
using LandBuilder.Domain;

namespace LandBuilder.Presentation;

/// <summary>
/// Scripted smoke test (run with <c>-- --selftest</c>): drives real input events through the viewport and checks the
/// game reacts. Uses an isolated save/leaderboard folder and exits with code 1 on any failure.
/// </summary>
public partial class Main
{
    private bool _selfTest;
    private readonly List<string> _failures = new();

    private async void RunSelfTest()
    {
        await Wait(0.5f);
        Log("starting");
        StartNewGame(777UL);
        await Wait(1.8f);

        // --- hover + rotate + click to place
        var best = GameEngine.FindBestPlacement(_session.State)!;
        var target = SlotScreen(best.Slot);
        Motion(target);
        await Wait(0.3f);
        Check(_hoverSlot == best.Slot, $"cursor over a candidate hovers it (hover={_hoverSlot}, want={best.Slot})");
        Check(_hoverEval is { IsValid: true }, "hover produces a placement preview");
        await Wait(0.4f);
        Snap("hover");

        for (var guard = 0; guard < 6 && _rotation != best.Rotation; guard++)
        {
            PressKey(Godot.Key.Q);
            await Wait(0.08f);
        }

        Check(_rotation == best.Rotation, "Q rotates the tile in hand");
        var before = _session.State.Board.Count;
        var scoreBefore = _session.State.Score;
        Click(target, MouseButton.Left);
        await Wait(1.2f);
        Check(_session.State.Board.Count == before + 1, "left click places the tile");
        Check(_board.Tiles.ContainsKey(best.Slot), "placed tile appears on the board");
        Check(_session.State.Score >= scoreBefore, "score never drops after a placement");
        Check(_hand is not null && _hand.Root.Visible, "a new tile arrives in hand");

        // --- undo
        PressKey(Godot.Key.Z, ctrl: true);
        await Wait(0.6f);
        Check(_session.State.Board.Count == before, "Ctrl+Z undoes the placement");
        Check(_session.State.UndosRemaining == GameRules.UndosPerRun - 1, "undo spends one charge");
        Check(!_board.Tiles.ContainsKey(best.Slot), "undone tile leaves the board");

        // --- right click rotates (without dragging)
        var rotation = _rotation;
        Click(SlotScreen(best.Slot), MouseButton.Right);
        await Wait(0.2f);
        Check(_rotation == GameEngine.NormalizeRotation(rotation - 1), "right click rotates the other way");

        // --- hint
        PressKey(Godot.Key.H);
        await Wait(0.3f);
        var hint = GameEngine.FindBestPlacement(_session.State)!;
        Check(_hintSlot == hint.Slot && _rotation == hint.Rotation, "H highlights the best spot and pre-rotates the tile");

        // --- left-drag pans instead of placing
        var count = _session.State.Board.Count;
        var focus = _rig.FocusTarget;
        var start = SlotScreen(hint.Slot);
        Press(start, MouseButton.Left, true);
        for (var i = 1; i <= 6; i++)
        {
            Motion(start + new Vector2(i * 20, i * 8), new Vector2(20, 8));
            await Wait(0.03f);
        }

        Press(start + new Vector2(120, 48), MouseButton.Left, false);
        await Wait(0.3f);
        Check(_session.State.Board.Count == count, "dragging does not place a tile");
        Check(_rig.FocusTarget.DistanceTo(focus) > 0.05f, "left-drag pans the camera");
        _rig.Reframe(IslandCentre(), IslandRadius(), instant: true);
        await Wait(0.2f);

        // --- clicking empty sea far away does nothing
        Click(new Vector2(40, 540), MouseButton.Left);
        await Wait(0.3f);
        Check(_session.State.Board.Count == count, "clicking open sea places nothing");

        // --- pause / resume
        PressKey(Godot.Key.Escape);
        await Wait(0.4f);
        Check(_mode == Mode.Paused, "Esc pauses");
        Snap("pause");
        _menus.OpenSettings();
        await Wait(0.4f);
        Snap("settings");
        _menus.Back();
        _menus.OpenHowTo();
        await Wait(0.4f);
        Snap("howto");
        _menus.Back();
        await Wait(0.3f);
        PressKey(Godot.Key.Escape);
        await Wait(0.4f);
        Check(_mode == Mode.Playing, "Esc again resumes");

        // --- play a dozen moves through real clicks
        for (var move = 0; move < 12 && !_session.State.IsGameOver; move++)
        {
            var plan = GameEngine.FindBestPlacement(_session.State)!;
            for (var guard = 0; guard < 6 && _rotation != plan.Rotation; guard++)
            {
                PressKey(Godot.Key.Q);
                await Wait(0.04f);
            }

            var p = SlotScreen(plan.Slot);
            Motion(p);
            await Wait(0.15f);
            Click(p, MouseButton.Left);
            await Wait(0.5f);
        }

        Check(_session.State.Board.Count >= count + 10, $"a dozen clicked moves land (board={_session.State.Board.Count})");
        await Wait(3.5f);
        Check(_shownScore == _session.State.Score, $"HUD score catches up with the real score ({_shownScore} vs {_session.State.Score})");

        // --- wonder card: pick up with G, cancel with Esc, pick up again and place by clicking
        _session.State.WonderCards.Add(WonderType.Cathedral);
        RefreshHud();
        PressKey(Godot.Key.G);
        await Wait(0.3f);
        Check(_wonderCard == 0 && _hand?.Wonder == WonderType.Cathedral, "G picks up the wonder card");
        PressKey(Godot.Key.Escape);
        await Wait(0.3f);
        Check(_wonderCard == -1 && _mode == Mode.Playing, "Esc puts the wonder card back without pausing");
        PressKey(Godot.Key.G);
        await Wait(0.3f);
        var wonderPlan = GameEngine.FindBestWonderPlacement(_session.State, 0)!;
        for (var guard = 0; guard < 6 && _rotation != wonderPlan.Rotation; guard++)
        {
            PressKey(Godot.Key.Q);
            await Wait(0.05f);
        }

        var wonderSpot = SlotScreen(wonderPlan.Slot);
        Motion(wonderSpot);
        await Wait(0.2f);
        Click(wonderSpot, MouseButton.Left);
        await Wait(1.0f);
        Check(_session.State.Board.TryGetValue(wonderPlan.Slot, out var site) && site.Wonder == WonderType.Cathedral, "clicking places the wonder site");
        Check(_session.State.WonderCards.Count == 0 && _wonderCard == -1, "placing uses up the card");
        Check(_board.Tiles.TryGetValue(wonderPlan.Slot, out var siteVisual) && siteVisual.WonderModel is not null, "the site shows a construction model");
        Snap("wonder_site");

        // --- save, back to title, continue
        var snapshot = _session.State.Fingerprint();
        GoToTitle();
        await Wait(0.6f);
        Check(_menus.IsTitleOpen, "main menu opens");
        ContinueGame();
        await Wait(0.6f);
        Check(_session.State.Fingerprint() == snapshot, "Continue restores the exact island");

        // --- force the end of the run
        // Bonus tiles from a perfect or quest can legitimately extend the run, so keep draining.
        for (var guard = 0; guard < 20 && !_session.State.IsGameOver; guard++)
        {
            _session.State.StackCount = 0;
            _session.State.NextTile = null;
            var last = GameEngine.FindBestPlacement(_session.State)!;
            Place(last.Slot, last.Rotation);
            await Wait(0.3f);
        }

        await Wait(3.2f);
        Check(_mode == Mode.GameOver && _menus.IsGameOverOpen, "running out of tiles shows the summary");
        Snap("gameover");
        Check(!System.IO.File.Exists(ProjectSettings.GlobalizePath(SavePath)), "finished run clears the save");

        Log(_failures.Count == 0 ? "ALL PASSED" : $"{_failures.Count} FAILED");
        GetTree().Quit(_failures.Count == 0 ? 0 : 1);
    }

    private void Snap(string name)
    {
        if (_captureDir is null)
            return;
        System.IO.Directory.CreateDirectory(_captureDir);
        GetViewport().GetTexture().GetImage().SavePng(System.IO.Path.Combine(_captureDir, $"{name}.png"));
    }

    private Vector2 SlotScreen(int slot) => _rig.Camera.UnprojectPosition(HexLayout.SlotToWorld(slot) + new Vector3(0, 0.05f, 0));

    private void Check(bool condition, string description)
    {
        GD.Print($"[selftest] {(condition ? "PASS" : "FAIL")}: {description}");
        if (!condition)
            _failures.Add(description);
    }

    private static void Log(string message) => GD.Print($"[selftest] {message}");

    private async Task Wait(float seconds) => await ToSignal(GetTree().CreateTimer(seconds), SceneTreeTimer.SignalName.Timeout);

    private void Motion(Vector2 position, Vector2? relative = null) =>
        GetViewport().PushInput(new InputEventMouseMotion { Position = position, GlobalPosition = position, Relative = relative ?? Vector2.Zero }, true);

    private void Press(Vector2 position, MouseButton button, bool pressed) =>
        GetViewport().PushInput(new InputEventMouseButton { Position = position, GlobalPosition = position, ButtonIndex = button, Pressed = pressed }, true);

    private void Click(Vector2 position, MouseButton button)
    {
        Motion(position);
        Press(position, button, true);
        Press(position, button, false);
    }

    private void PressKey(Key key, bool ctrl = false)
    {
        GetViewport().PushInput(new InputEventKey { Keycode = key, Pressed = true, CtrlPressed = ctrl });
        GetViewport().PushInput(new InputEventKey { Keycode = key, Pressed = false, CtrlPressed = ctrl });
    }
}
