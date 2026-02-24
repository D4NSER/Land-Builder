using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using LandBuilder.Application;
using LandBuilder.Domain;
using LandBuilder.Infrastructure;

namespace LandBuilder.Presentation;

public partial class MainController : Node3D
{
	private const ulong DefaultSeed = 12345UL;
	private const string DefaultSavePath = "user://save.json";
	private const string DefaultHighScorePath = "user://highscore.json";
	private const float HexRadius = 1.1f;
	private const float HexSpacingX = 2.0f;
	private const float HexSpacingZ = 1.72f;

	private readonly SaveRepository _saveRepository = new();
	private readonly HighScoreRepository _highScoreRepository = new();
	private readonly UiEventSink _eventSink = new();
	private readonly FastNoiseLite _terrainNoise = new();
	private readonly Dictionary<int, SlotVisual> _slots = new();
	private readonly Dictionary<TileType, StandardMaterial3D> _solidTileMaterials = new();
	private readonly Dictionary<TileType, StandardMaterial3D> _previewTileMaterials = new();
	private readonly Dictionary<int, TileVisualSignature> _renderedTiles = new();
	private readonly Dictionary<int, List<RippleAnim>> _slotRipples = new();

	private GameSession _session = null!;
	private Node3D _boardRoot = null!;
	private Node3D _cameraPivot = null!;
	private Camera3D _camera = null!;
	private DirectionalLight3D _keyLight = null!;
	private DirectionalLight3D _fillLight = null!;
	private OmniLight3D _bounceLight = null!;
	private Control _hudRoot = null!;
	private PanelContainer _topLeftPanel = null!;
	private PanelContainer _tileDeckPanel = null!;
	private HBoxContainer _bottomCenterBar = null!;

	private Label _coinsLabel = null!;
	private Label _scoreLabel = null!;
	private Label _highScoreLabel = null!;
	private Label _currentTileLabel = null!;
	private Label _rotationLabel = null!;
	private Label _unlockedTilesLabel = null!;
	private Label _lastMessageLabel = null!;
	private Label _statusLabel = null!;
	private PanelContainer _tileSwatchPanel = null!;
	private ColorRect _tileSwatchColor = null!;
    private Label _tutorialHintLabel = null!;
    private PanelContainer _tutorialHintPanel = null!;
    private RichTextLabel _eventLog = null!;
    private Label _eventTitleLabel = null!;
    private PanelContainer _actionsRailPanel = null!;
    private PanelContainer _eventPanel = null!;
    private Button _utilityButton = null!;
    private Button _drawButton = null!;
    private Button _submitScoreButton = null!;
    private Button _saveButton = null!;
    private Button _loadButton = null!;
    private Button _refreshHighScoreButton = null!;
    private LineEdit _seedEdit = null!;
	private LineEdit _savePathEdit = null!;
	private VBoxContainer _unlockButtons = null!;
	private PanelContainer _unlockPanel = null!;
	private Button _unlockToggleButton = null!;
	private Label _hintArrowLabel = null!;
	private Vector2 _hintArrowBasePosition;

	private Node3D _previewRoot = null!;
	private Node3D _previewVisual = null!;
	private bool _hasPreviewVisual;
	private TileType? _previewTileType;
	private Node3D _decorRoot = null!;
	private Node3D _hoverRingRoot = null!;
	private StandardMaterial3D _hoverRingMaterial = null!;
	private Tween _hoverRingTween = null!;
	private Color _hoverRingBaseColor = new(1f, 1f, 1f, 0.0f);
	private float _hoverRingAlpha;
	private float _time;

	private int _rotationQuarterTurns;
	private int? _hoveredSlotIndex;
	private bool _showEmptyOverlays;
	private bool _orbitDragging;
	private bool _cameraUserAdjusted;
	private Vector2 _lastMousePos;
	private Vector3 _cameraFocusOffset;
	private Vector3 _cameraFocusSmoothedOffset;
	private int? _placementFocusSlot;
	private float _placementFocusTimer;
	private float _cameraYaw = 0.75f;
	private float _cameraPitch = -0.72f;
    private float _cameraDistance = 14.0f;
    private Vector3 _cameraSmoothedPosition;
    private bool _cameraPositionInitialized;
    private float _keyBaseEnergy;
    private Vector3 _keyBaseRotation;
    private Color _keyBaseColor;
    private Color _fillBaseColor;
    private Color _bounceBaseColor;
    private bool _utilityPanelVisible;
    private readonly bool _minimalPlayMode = true;
    private float _statusErrorFlashTimer;
    private Vector2 _lastHudViewportSize;
    private float _perfSampleTimer;
    private int _lastMeshInstanceCount;
    private int _lastNodeCount;
    private bool _perfWarned;

	public float HoverRingAlpha
	{
		get => _hoverRingAlpha;
		set
		{
			_hoverRingAlpha = value;
			if (_hoverRingMaterial is not null)
				_hoverRingMaterial.AlbedoColor = new Color(_hoverRingBaseColor.R, _hoverRingBaseColor.G, _hoverRingBaseColor.B, value);
			if (_hoverRingRoot is not null)
				_hoverRingRoot.Visible = value > 0.01f;
		}
	}

	public override void _Ready()
	{
		_boardRoot = GetNode<Node3D>("BoardRoot");
		_cameraPivot = GetNode<Node3D>("CameraPivot");
		_camera = GetNode<Camera3D>("CameraPivot/Camera3D");
        _keyLight = GetNode<DirectionalLight3D>("DirectionalLight3D");
        _fillLight = GetNode<DirectionalLight3D>("FillLight");
        _bounceLight = GetNode<OmniLight3D>("BounceLight");
        _hudRoot = GetNode<Control>("CanvasLayer/HUDRoot");
        _topLeftPanel = GetNode<PanelContainer>("CanvasLayer/HUDRoot/TopLeftPanel");
        _tileDeckPanel = GetNode<PanelContainer>("CanvasLayer/HUDRoot/TileDeckPanel");
        _bottomCenterBar = GetNode<HBoxContainer>("CanvasLayer/HUDRoot/BottomCenter");
        _keyBaseEnergy = _keyLight.LightEnergy;
        _keyBaseRotation = _keyLight.Rotation;
        _keyBaseColor = _keyLight.LightColor;
        _fillBaseColor = _fillLight.LightColor;
        _bounceBaseColor = _bounceLight.LightColor;

        _coinsLabel = GetNode<Label>("CanvasLayer/HUDRoot/TopLeftPanel/CurrencyRow/CoinsChip/CoinsLabel");
        _scoreLabel = GetNode<Label>("CanvasLayer/HUDRoot/TopLeftPanel/CurrencyRow/ScoreChip/ScoreLabel");
        _highScoreLabel = GetNode<Label>("CanvasLayer/HUDRoot/TopLeftPanel/CurrencyRow/HighScoreChip/HighScoreLabel");
        _utilityButton = GetNode<Button>("CanvasLayer/HUDRoot/TopLeftPanel/CurrencyRow/UtilityButton");
        _tileSwatchPanel = GetNode<PanelContainer>("CanvasLayer/HUDRoot/TileDeckPanel/DeckVBox/TileCardRow/TileSwatchPanel");
        _tileSwatchColor = GetNode<ColorRect>("CanvasLayer/HUDRoot/TileDeckPanel/DeckVBox/TileCardRow/TileSwatchPanel/TileSwatchColor");
        _currentTileLabel = GetNode<Label>("CanvasLayer/HUDRoot/TileDeckPanel/DeckVBox/TileCardRow/TileCardText/CurrentTileLabel");
		_rotationLabel = GetNode<Label>("CanvasLayer/HUDRoot/TileDeckPanel/DeckVBox/TileCardRow/TileCardText/RotationLabel");
		_unlockedTilesLabel = GetNode<Label>("CanvasLayer/HUDRoot/TileDeckPanel/DeckVBox/UnlockedTilesLabel");
		_tutorialHintPanel = GetNode<PanelContainer>("CanvasLayer/HUDRoot/TileDeckPanel/DeckVBox/TutorialHintPanel");
		_tutorialHintLabel = GetNode<Label>("CanvasLayer/HUDRoot/TileDeckPanel/DeckVBox/TutorialHintPanel/TutorialHintLabel");
		_lastMessageLabel = GetNode<Label>("CanvasLayer/HUDRoot/TileDeckPanel/DeckVBox/LastMessageLabel");
        _statusLabel = GetNode<Label>("CanvasLayer/HUDRoot/TileDeckPanel/DeckVBox/StatusLabel");
        _eventLog = GetNode<RichTextLabel>("CanvasLayer/HUDRoot/EventPanel/EventVBox/EventLog");
        _eventTitleLabel = GetNode<Label>("CanvasLayer/HUDRoot/EventPanel/EventVBox/EventTitle");
        _actionsRailPanel = GetNode<PanelContainer>("CanvasLayer/HUDRoot/ActionsRail");
        _eventPanel = GetNode<PanelContainer>("CanvasLayer/HUDRoot/EventPanel");
        _drawButton = GetNode<Button>("CanvasLayer/HUDRoot/ActionsRail/RailVBox/DrawButton");
        _submitScoreButton = GetNode<Button>("CanvasLayer/HUDRoot/ActionsRail/RailVBox/SubmitScoreButton");
        _saveButton = GetNode<Button>("CanvasLayer/HUDRoot/ActionsRail/RailVBox/SaveButton");
        _loadButton = GetNode<Button>("CanvasLayer/HUDRoot/ActionsRail/RailVBox/LoadButton");
        _refreshHighScoreButton = GetNode<Button>("CanvasLayer/HUDRoot/ActionsRail/RailVBox/RefreshHighScoreButton");
        _seedEdit = GetNode<LineEdit>("CanvasLayer/HUDRoot/ActionsRail/RailVBox/SeedRow/SeedEdit");
		_savePathEdit = GetNode<LineEdit>("CanvasLayer/HUDRoot/ActionsRail/RailVBox/SavePathEdit");
		_unlockButtons = GetNode<VBoxContainer>("CanvasLayer/HUDRoot/ActionsRail/RailVBox/UnlockPanel/UnlockScroll/UnlockButtons");
		_unlockPanel = GetNode<PanelContainer>("CanvasLayer/HUDRoot/ActionsRail/RailVBox/UnlockPanel");
		_unlockToggleButton = GetNode<Button>("CanvasLayer/HUDRoot/ActionsRail/RailVBox/UnlockToggleButton");
		_hintArrowLabel = GetNode<Label>("CanvasLayer/HUDRoot/HintArrow");
		_hintArrowBasePosition = _hintArrowLabel.Position;

		_seedEdit.Text = DefaultSeed.ToString();
		_savePathEdit.Text = DefaultSavePath;
		_terrainNoise.NoiseType = FastNoiseLite.NoiseTypeEnum.Simplex;
		_terrainNoise.Frequency = 0.65f;

		_decorRoot = new Node3D { Name = "DecorRoot" };
		AddChild(_decorRoot);
		_previewRoot = new Node3D { Name = "PreviewRoot" };
		_boardRoot.AddChild(_previewRoot);

		BuildBackdropDecor();
		BuildBoardVisuals();
		BuildHoverRing();
		BuildUnlockButtons();
		WireUi();
        ApplyHudStyle();
        ApplyResponsiveHudLayout(force: true);
        ApplyUtilityPanelState(false);
        FocusCamera();
		StartNewSession(DefaultSeed);
		RefreshHighScoreLabel();
		Render();
	}

	public override void _Process(double delta)
	{
		_time += (float)delta;
		UpdateHoverSlot();
		UpdateCamera();
		UpdateDaylightDrift();
		UpdateIdleMotion();
		UpdateWaterRipples();
		UpdateHintArrowPulse();
		UpdateStatusFeedback((float)delta);
		UpdateCameraFocusOffset((float)delta);
		UpdatePerformanceMetrics((float)delta);
		ApplyResponsiveHudLayout();
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		switch (@event)
		{
			case InputEventKey key when key.Pressed && !key.Echo:
				HandleKeyboard(key);
				break;
			case InputEventMouseButton mouseButton:
				HandleMouseButton(mouseButton);
				break;
			case InputEventMouseMotion motion:
				HandleMouseMotion(motion);
				break;
		}
	}

	private void HandleKeyboard(InputEventKey key)
	{
        if (key.Keycode == Key.Q)
		{
			_rotationQuarterTurns = ((_rotationQuarterTurns - 1) % 4 + 4) % 4;
			Render();
			GetViewport().SetInputAsHandled();
			return;
		}

        if (key.Keycode == Key.E)
		{
			_rotationQuarterTurns = (_rotationQuarterTurns + 1) % 4;
			Render();
			GetViewport().SetInputAsHandled();
			return;
		}

        if (key.Keycode == Key.Space)
        {
            Issue(new DrawTileCommand());
            GetViewport().SetInputAsHandled();
            return;
        }

        if (key.Keycode == Key.Enter || key.Keycode == Key.KpEnter)
        {
            Issue(new SubmitScoreCommand());
            GetViewport().SetInputAsHandled();
            return;
        }

        if (key.Keycode == Key.Tab)
        {
            ApplyUtilityPanelState(!_utilityPanelVisible);
            GetViewport().SetInputAsHandled();
            return;
        }

        if (key.Keycode == Key.F)
		{
			FocusCamera();
			Render();
			GetViewport().SetInputAsHandled();
			return;
		}

		if (key.Keycode == Key.T)
		{
			_showEmptyOverlays = !_showEmptyOverlays;
			SetStatus($"Empty overlays {(_showEmptyOverlays ? "on" : "off")}.");
			Render();
			GetViewport().SetInputAsHandled();
		}
	}

	private void HandleMouseButton(InputEventMouseButton mouseButton)
	{
		if (!mouseButton.Pressed)
		{
			if (mouseButton.ButtonIndex == MouseButton.Right)
				_orbitDragging = false;
			return;
		}

		_lastMousePos = mouseButton.Position;

		if (mouseButton.ButtonIndex == MouseButton.WheelUp)
		{
			_cameraDistance = Mathf.Clamp(_cameraDistance - 0.8f, 7.5f, 22.0f);
			_cameraUserAdjusted = true;
			UpdateCamera();
			GetViewport().SetInputAsHandled();
			return;
		}

		if (mouseButton.ButtonIndex == MouseButton.WheelDown)
		{
			_cameraDistance = Mathf.Clamp(_cameraDistance + 0.8f, 7.5f, 22.0f);
			_cameraUserAdjusted = true;
			UpdateCamera();
			GetViewport().SetInputAsHandled();
			return;
		}

		if (mouseButton.ButtonIndex == MouseButton.Right)
		{
			_orbitDragging = true;
			GetViewport().SetInputAsHandled();
			return;
		}

		if (mouseButton.ButtonIndex == MouseButton.Left)
			TryPlaceFromScreenPoint(mouseButton.Position);
	}

	private void HandleMouseMotion(InputEventMouseMotion motion)
	{
		_lastMousePos = motion.Position;
		if (!_orbitDragging)
			return;

		_cameraYaw -= motion.Relative.X * 0.008f;
		_cameraPitch = Mathf.Clamp(_cameraPitch - motion.Relative.Y * 0.006f, -1.15f, -0.35f);
		_cameraUserAdjusted = true;
		UpdateCamera();
		GetViewport().SetInputAsHandled();
	}

	private void WireUi()
	{
		_drawButton.Pressed += () => Issue(new DrawTileCommand());
		_submitScoreButton.Pressed += () => Issue(new SubmitScoreCommand());
		_saveButton.Pressed += SaveGame;
		_loadButton.Pressed += LoadGame;
        _refreshHighScoreButton.Pressed += RefreshHighScoreLabel;
        GetNode<Button>("CanvasLayer/HUDRoot/ActionsRail/RailVBox/SeedRow/NewGameButton").Pressed += CreateNewGameFromSeed;
        _utilityButton.Pressed += () => ApplyUtilityPanelState(!_utilityPanelVisible);
        GetNode<Button>("CanvasLayer/HUDRoot/BottomCenter/FocusButton").Pressed += () =>
        {
            FocusCamera();
			Render();
		};

        _unlockToggleButton.Toggled += pressed =>
        {
            _unlockPanel.Visible = pressed;
            _unlockToggleButton.Text = pressed ? "Hide Unlocks" : "Show Unlocks";
        };
    }

    private void ApplyUtilityPanelState(bool visible)
    {
        _utilityPanelVisible = visible;
        _actionsRailPanel.Visible = !_minimalPlayMode || visible;
        _eventPanel.Visible = !_minimalPlayMode || visible;

        if (_minimalPlayMode)
        {
            _unlockPanel.Visible = false;
            _unlockToggleButton.Visible = false;
        }

        _utilityButton.Text = _utilityPanelVisible ? "Close" : "Menu";
		ApplyResponsiveHudLayout(force: true);
    }

	private void ApplyHudStyle()
	{
		var chipPaths = new[]
		{
			"CanvasLayer/HUDRoot/TopLeftPanel/CurrencyRow/CoinsChip",
			"CanvasLayer/HUDRoot/TopLeftPanel/CurrencyRow/ScoreChip",
            "CanvasLayer/HUDRoot/TopLeftPanel/CurrencyRow/HighScoreChip"
		};
		foreach (var path in chipPaths)
			StylePanel(GetNode<PanelContainer>(path), new Color(1f, 1f, 1f, 0.76f), 16, 10, 10, 10, 10);

		StylePanel(GetNode<PanelContainer>("CanvasLayer/HUDRoot/TopLeftPanel"), new Color(1f, 1f, 1f, 0.25f), 18, 6, 6, 6, 6);
		StylePanel(GetNode<PanelContainer>("CanvasLayer/HUDRoot/ActionsRail"), new Color(1f, 1f, 1f, 0.64f), 22, 12, 12, 12, 12);
		StylePanel(GetNode<PanelContainer>("CanvasLayer/HUDRoot/TileDeckPanel"), new Color(1f, 1f, 1f, 0.70f), 22, 12, 12, 12, 12);
		StylePanel(GetNode<PanelContainer>("CanvasLayer/HUDRoot/EventPanel"), new Color(1f, 1f, 1f, 0.55f), 18, 8, 8, 8, 8);
		StylePanel(_tileSwatchPanel, new Color(1f, 1f, 1f, 0.75f), 12, 4, 4, 4, 4);
		StylePanel(_unlockPanel, new Color(1f, 1f, 1f, 0.45f), 14, 6, 6, 6, 6);
		StylePanel(_tutorialHintPanel, new Color(1f, 0.98f, 0.85f, 0.80f), 14, 8, 8, 8, 8);

		_eventLog.ScrollFollowing = true;
		_eventLog.CustomMinimumSize = new Vector2(0, 72);
		_eventLog.AddThemeFontSizeOverride("normal_font_size", 10);
		_eventLog.AddThemeColorOverride("default_color", new Color(0.16f, 0.20f, 0.23f, 0.92f));
		_currentTileLabel.AddThemeFontSizeOverride("font_size", 15);
		_rotationLabel.AddThemeColorOverride("font_color", new Color(0.24f, 0.28f, 0.33f));
		_lastMessageLabel.AddThemeColorOverride("font_color", new Color(0.20f, 0.24f, 0.28f));
		_statusLabel.AddThemeColorOverride("font_color", new Color(0.20f, 0.24f, 0.28f));
		_tutorialHintLabel.AddThemeColorOverride("font_color", new Color(0.30f, 0.28f, 0.18f));
		StyleRailButton(_drawButton, new Color(0.82f, 0.94f, 0.84f), emphasize: true);
		StyleRailButton(_submitScoreButton, new Color(0.88f, 0.90f, 0.98f));
		StyleRailButton(_saveButton, new Color(0.93f, 0.93f, 0.96f));
		StyleRailButton(_loadButton, new Color(0.93f, 0.93f, 0.96f));
		StyleRailButton(_refreshHighScoreButton, new Color(0.94f, 0.95f, 0.90f));
		foreach (var labelPath in new[]
		{
			"CanvasLayer/HUDRoot/ActionsRail/RailVBox/PlayToolsLabel",
			"CanvasLayer/HUDRoot/ActionsRail/RailVBox/SessionToolsLabel",
			"CanvasLayer/HUDRoot/ActionsRail/RailVBox/StorageToolsLabel"
		})
		{
			var label = GetNode<Label>(labelPath);
			label.AddThemeColorOverride("font_color", new Color(0.28f, 0.31f, 0.36f, 0.95f));
			label.AddThemeFontSizeOverride("font_size", 11);
		}
		_tutorialHintLabel.Text = "Tap Draw, then click a hex to place. Q/E rotates. RMB orbits, wheel zooms.";
		_hintArrowLabel.Modulate = new Color(1f, 0.98f, 0.85f, 0.0f);
	}

	private void ApplyResponsiveHudLayout(bool force = false)
	{
		if (_hudRoot is null)
			return;

		var size = GetViewport().GetVisibleRect().Size;
		if (!force && size == _lastHudViewportSize)
			return;

		_lastHudViewportSize = size;

		var safeMargin = Mathf.Clamp(size.X * 0.012f, 10f, 28f);
		var topMargin = Mathf.Clamp(size.Y * 0.012f, 10f, 22f);
		var panelGap = Mathf.Clamp(size.Y * 0.014f, 10f, 18f);

		var leftWidth = Mathf.Clamp(size.X * 0.17f, 208f, 288f);
		var topLeftWidth = Mathf.Clamp(size.X * 0.24f, 320f, 460f);
		var tileDeckWidth = Mathf.Clamp(size.X * 0.24f, 300f, 420f);
		var tileDeckHeight = Mathf.Clamp(size.Y * 0.33f, 278f, 360f);
		var actionsHeight = Mathf.Clamp(size.Y * 0.54f, 360f, 640f);
		var eventHeight = Mathf.Clamp(size.Y * 0.18f, 120f, 200f);
		if (_utilityPanelVisible)
			eventHeight = Mathf.Clamp(eventHeight + 18f, 138f, 230f);

		_topLeftPanel.OffsetLeft = safeMargin;
		_topLeftPanel.OffsetTop = topMargin;
		_topLeftPanel.OffsetRight = safeMargin + topLeftWidth;
		_topLeftPanel.OffsetBottom = topMargin + 70f;

		_actionsRailPanel.OffsetLeft = safeMargin;
		_actionsRailPanel.OffsetTop = _topLeftPanel.OffsetBottom + panelGap;
		_actionsRailPanel.OffsetRight = safeMargin + leftWidth;
		_actionsRailPanel.OffsetBottom = _actionsRailPanel.OffsetTop + actionsHeight;

		_eventPanel.OffsetLeft = safeMargin;
		_eventPanel.OffsetRight = safeMargin + Mathf.Max(leftWidth, topLeftWidth * 0.9f);
		_eventPanel.OffsetBottom = size.Y - safeMargin;
		_eventPanel.OffsetTop = _eventPanel.OffsetBottom - eventHeight;

		// Avoid collisions between left utility/event panels on shorter windows.
		if (_eventPanel.OffsetTop < _actionsRailPanel.OffsetBottom + panelGap)
		{
			_eventPanel.OffsetTop = _actionsRailPanel.OffsetBottom + panelGap;
			_eventPanel.OffsetBottom = Mathf.Min(size.Y - safeMargin, _eventPanel.OffsetTop + eventHeight);
		}

		_tileDeckPanel.OffsetLeft = -tileDeckWidth - safeMargin;
		_tileDeckPanel.OffsetTop = topMargin;
		_tileDeckPanel.OffsetRight = -safeMargin;
		_tileDeckPanel.OffsetBottom = topMargin + tileDeckHeight;

		var bottomWidth = Mathf.Clamp(size.X * 0.22f, 180f, 280f);
		var bottomHeight = 50f;
		_bottomCenterBar.OffsetLeft = -bottomWidth * 0.5f;
		_bottomCenterBar.OffsetRight = bottomWidth * 0.5f;
		_bottomCenterBar.OffsetBottom = -safeMargin;
		_bottomCenterBar.OffsetTop = -safeMargin - bottomHeight;

		// Place hint arrow near the draw button in utility panel when visible, otherwise near left margin.
		var hintX = _actionsRailPanel.OffsetLeft + 170f;
		var hintY = _actionsRailPanel.OffsetTop + 14f;
		_hintArrowBasePosition = new Vector2(hintX, hintY);

		ApplyCameraFramingForViewport();
	}

	private void ApplyCameraFramingForViewport()
	{
		if (_cameraUserAdjusted)
			return;

		var targetDistance = GetDefaultCameraDistanceForViewport();
		var targetPitch = GetDefaultCameraPitchForViewport();

		_cameraDistance = targetDistance;
		_cameraPitch = targetPitch;
		if (!_cameraPositionInitialized)
			UpdateCamera();
	}

	private static void StyleRailButton(Button button, Color tint, bool emphasize = false)
	{
		button.Modulate = tint;
		var style = new StyleBoxFlat
		{
			BgColor = new Color(1f, 1f, 1f, emphasize ? 0.84f : 0.72f),
			CornerRadiusBottomLeft = 12,
			CornerRadiusBottomRight = 12,
			CornerRadiusTopLeft = 12,
			CornerRadiusTopRight = 12,
			BorderWidthBottom = 1,
			BorderWidthTop = 1,
			BorderWidthLeft = 1,
			BorderWidthRight = 1,
			BorderColor = new Color(1f, 1f, 1f, emphasize ? 0.72f : 0.52f)
		};
		button.AddThemeStyleboxOverride("normal", style);

		var hover = (StyleBoxFlat)style.Duplicate();
		hover.BgColor = hover.BgColor.Lightened(0.04f);
		hover.BorderColor = hover.BorderColor.Lightened(0.08f);
		button.AddThemeStyleboxOverride("hover", hover);

		var pressed = (StyleBoxFlat)style.Duplicate();
		pressed.BgColor = pressed.BgColor.Darkened(0.04f);
		button.AddThemeStyleboxOverride("pressed", pressed);

		var disabled = (StyleBoxFlat)style.Duplicate();
		disabled.BgColor = new Color(1f, 1f, 1f, 0.45f);
		disabled.BorderColor = new Color(1f, 1f, 1f, 0.25f);
		button.AddThemeStyleboxOverride("disabled", disabled);
	}

	private static void StylePanel(
		PanelContainer panel,
		Color color,
		int radius,
		int marginLeft,
		int marginTop,
		int marginRight,
		int marginBottom)
	{
		var style = new StyleBoxFlat
		{
			BgColor = color,
			CornerRadiusBottomLeft = radius,
			CornerRadiusBottomRight = radius,
			CornerRadiusTopLeft = radius,
			CornerRadiusTopRight = radius
		};
		style.SetContentMargin(Side.Left, marginLeft);
		style.SetContentMargin(Side.Top, marginTop);
		style.SetContentMargin(Side.Right, marginRight);
		style.SetContentMargin(Side.Bottom, marginBottom);
		panel.AddThemeStyleboxOverride("panel", style);
	}

	private void BuildUnlockButtons()
	{
		foreach (var child in _unlockButtons.GetChildren())
			child.QueueFree();

		foreach (var tile in GameState.AllTileTypes.OrderBy(t => t))
		{
			if (tile == TileType.Plains)
				continue;

			var cost = GameState.UnlockCosts.TryGetValue(tile, out var value) ? value : 0;
			var button = new Button
			{
				Text = $"Unlock {tile} ({cost})",
				SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
			};

			var capturedTile = tile;
			button.Pressed += () => Issue(new UnlockTileCommand(capturedTile));
			_unlockButtons.AddChild(button);
		}
	}

	private void BuildBoardVisuals()
	{
		foreach (var child in _boardRoot.GetChildren())
		{
			if (child != _previewRoot)
				child.QueueFree();
		}

		_slots.Clear();
		_renderedTiles.Clear();

		for (var slot = 0; slot < GameState.BoardWidth * GameState.BoardHeight; slot++)
		{
			var position = GetSlotPosition(slot);
			var root = new Node3D { Name = $"Slot{slot}", Position = position };
			_boardRoot.AddChild(root);

			var baseHexMaterial = new StandardMaterial3D
			{
				AlbedoColor = new Color(0.76f, 0.90f, 0.95f),
				Roughness = 0.95f
			};

			var baseHex = new MeshInstance3D
			{
				Name = "BaseHex",
				Mesh = new CylinderMesh
				{
					Height = 0.18f,
					TopRadius = HexRadius,
					BottomRadius = HexRadius * 0.98f,
					RadialSegments = 6
				},
				Rotation = new Vector3(0, Mathf.Pi / 6.0f, 0)
			};
			baseHex.MaterialOverride = baseHexMaterial;
			root.AddChild(baseHex);

			var overlay = BuildPlusOverlay();
			overlay.Name = "Overlay";
			overlay.Position = new Vector3(0, 0.13f, 0);
			root.AddChild(overlay);

			var body = new StaticBody3D { Name = "ClickBody" };
			body.SetMeta("slot_index", slot);
			root.AddChild(body);

			var collision = new CollisionShape3D
			{
				Shape = new CylinderShape3D { Height = 0.45f, Radius = HexRadius * 0.92f },
				Position = new Vector3(0, 0.18f, 0)
			};
			body.AddChild(collision);

			var tileRoot = new Node3D { Name = "TileRoot", Position = new Vector3(0, 0.12f, 0) };
			root.AddChild(tileRoot);

			_slots[slot] = new SlotVisual(slot, root, baseHex, baseHexMaterial, overlay, tileRoot, tileRoot.Position.Y, slot * 0.67f);
		}
	}

	private void BuildBackdropDecor()
	{
		foreach (var child in _decorRoot.GetChildren())
			child.QueueFree();

		var water = new MeshInstance3D
		{
			Name = "WaterPlane",
			Mesh = new BoxMesh { Size = new Vector3(34f, 0.25f, 34f) },
			Position = new Vector3(0, -0.28f, 0)
		};
		water.MaterialOverride = new StandardMaterial3D
		{
			AlbedoColor = new Color(0.45f, 0.78f, 0.90f),
			Metallic = 0.10f,
			Roughness = 0.18f
		};
		_decorRoot.AddChild(water);

		var hazeWater = new MeshInstance3D
		{
			Name = "WaterHazeLayer",
			Mesh = new BoxMesh { Size = new Vector3(38f, 0.06f, 38f) },
			Position = new Vector3(0, -0.18f, 0)
		};
		hazeWater.MaterialOverride = new StandardMaterial3D
		{
			AlbedoColor = new Color(0.62f, 0.82f, 0.90f, 0.45f),
			Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
			Metallic = 0.02f,
			Roughness = 0.55f
		};
		_decorRoot.AddChild(hazeWater);

		var shoreRoot = new Node3D { Name = "ShoreRing" };
		_decorRoot.AddChild(shoreRoot);
		var shoreMat = new StandardMaterial3D { AlbedoColor = new Color(0.88f, 0.80f, 0.66f), Roughness = 0.95f };
		var rockMat = new StandardMaterial3D { AlbedoColor = new Color(0.67f, 0.70f, 0.73f), Roughness = 0.98f };
		var rng = new RandomNumberGenerator { Seed = 20260224 };

		var slots = Enumerable.Range(0, GameState.BoardWidth * GameState.BoardHeight).Select(GetSlotPosition).ToArray();
		var outer = slots.Max(p => new Vector2(p.X, p.Z).Length()) + 2.6f;
		for (var i = 0; i < 18; i++)
		{
			var a = (Mathf.Tau / 18f) * i + rng.RandfRange(-0.12f, 0.12f);
			var r = outer + rng.RandfRange(-0.45f, 0.65f);
			var pos = new Vector3(Mathf.Cos(a) * r, -0.06f + rng.RandfRange(-0.02f, 0.02f), Mathf.Sin(a) * r);
			var blob = new MeshInstance3D
			{
				Mesh = (i % 2 == 0)
					? new SphereMesh { Radius = rng.RandfRange(0.45f, 0.85f), Height = rng.RandfRange(0.20f, 0.40f), RadialSegments = 10, Rings = 6 }
					: new CylinderMesh { TopRadius = rng.RandfRange(0.35f, 0.70f), BottomRadius = rng.RandfRange(0.45f, 0.80f), Height = rng.RandfRange(0.16f, 0.32f), RadialSegments = 8 },
				Position = pos,
				Rotation = new Vector3(0, a, 0),
				Scale = new Vector3(rng.RandfRange(0.8f, 1.3f), 1.0f, rng.RandfRange(0.7f, 1.25f))
			};
			var baseMat = (i % 3 == 0) ? rockMat : shoreMat;
			var dist01 = Mathf.Clamp((new Vector2(pos.X, pos.Z).Length() - outer * 0.75f) / (outer * 0.45f), 0f, 1f);
			var tintScale = Mathf.Lerp(1.00f, 0.82f, dist01);
			blob.MaterialOverride = new StandardMaterial3D
			{
				AlbedoColor = new Color(baseMat.AlbedoColor.R * tintScale, baseMat.AlbedoColor.G * tintScale, baseMat.AlbedoColor.B * tintScale),
				Roughness = baseMat.Roughness
			};
			shoreRoot.AddChild(blob);
		}
	}

	private void UpdateDaylightDrift()
	{
		if (_keyLight is null)
			return;

		var energyOffset = (Mathf.Sin(_time * 0.15f) * 0.08f) + (Mathf.Sin((_time * 0.07f) + 1.7f) * 0.05f);
		_keyLight.LightEnergy = _keyBaseEnergy + energyOffset;

        var yawOffset = (Mathf.Sin(_time * 0.11f) * 0.02f);
        var pitchOffset = (Mathf.Sin((_time * 0.09f) + 0.8f) * 0.01f);
        _keyLight.Rotation = new Vector3(_keyBaseRotation.X + pitchOffset, _keyBaseRotation.Y + yawOffset, _keyBaseRotation.Z);

        var warmth = (Mathf.Sin((_time * 0.06f) + 0.9f) * 0.015f);
        _keyLight.LightColor = new Color(
            Mathf.Clamp(_keyBaseColor.R + warmth * 0.4f, 0f, 1f),
            Mathf.Clamp(_keyBaseColor.G - warmth * 0.3f, 0f, 1f),
            Mathf.Clamp(_keyBaseColor.B - warmth * 0.7f, 0f, 1f),
            1f);

        // Keep fill and bounce gently coherent but even subtler.
        _fillLight.LightEnergy = 0.28f + (Mathf.Sin((_time * 0.12f) + 2.4f) * 0.015f);
        _bounceLight.LightEnergy = 0.35f + (Mathf.Sin((_time * 0.10f) + 1.1f) * 0.02f);
        _fillLight.LightColor = new Color(
            Mathf.Clamp(_fillBaseColor.R - warmth * 0.2f, 0f, 1f),
            Mathf.Clamp(_fillBaseColor.G, 0f, 1f),
            Mathf.Clamp(_fillBaseColor.B + warmth * 0.25f, 0f, 1f),
            1f);
        _bounceLight.LightColor = new Color(
            Mathf.Clamp(_bounceBaseColor.R + warmth * 0.2f, 0f, 1f),
            Mathf.Clamp(_bounceBaseColor.G, 0f, 1f),
            Mathf.Clamp(_bounceBaseColor.B - warmth * 0.15f, 0f, 1f),
            1f);
    }

	private void BuildHoverRing()
	{
		if (_hoverRingRoot is not null)
			_hoverRingRoot.QueueFree();

		_hoverRingRoot = new Node3D { Name = "HoverRing", Visible = false };
		_hoverRingMaterial = new StandardMaterial3D
		{
			AlbedoColor = _hoverRingBaseColor,
			Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
			ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded
		};

		const int segments = 18;
		const float radius = 1.05f;
		for (var i = 0; i < segments; i++)
		{
			var angle = Mathf.Tau * i / segments;
			var seg = new MeshInstance3D
			{
				Mesh = new BoxMesh { Size = new Vector3(0.24f, 0.015f, 0.05f) },
				Position = new Vector3(Mathf.Cos(angle) * radius, 0.22f, Mathf.Sin(angle) * radius),
				Rotation = new Vector3(0, -angle, 0),
				MaterialOverride = _hoverRingMaterial
			};
			_hoverRingRoot.AddChild(seg);
		}

		_boardRoot.AddChild(_hoverRingRoot);
		HoverRingAlpha = 0f;
	}

	private static Node3D BuildPlusOverlay()
	{
		var overlay = new Node3D();
		var mat = new StandardMaterial3D
		{
			AlbedoColor = new Color(1, 1, 1, 0.32f),
			Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
			ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded
		};

		foreach (var (size, pos) in new[]
		{
			(new Vector3(0.55f, 0.02f, 0.09f), new Vector3(0, 0, 0)),
			(new Vector3(0.09f, 0.02f, 0.55f), new Vector3(0, 0, 0))
		})
		{
			var bar = new MeshInstance3D
			{
				Mesh = new BoxMesh { Size = size },
				Position = pos
			};
			bar.MaterialOverride = mat;
			overlay.AddChild(bar);
		}

		return overlay;
	}

	private void StartNewSession(ulong seed)
	{
		_eventSink.Clear();
		_rotationQuarterTurns = 0;
		_hoveredSlotIndex = null;
		AnimateHoverRing(false);
		_session = new GameSession(
			GameState.CreateInitial(seed),
			_eventSink,
			_highScoreRepository,
			ResolveHighScorePath());

		_renderedTiles.Clear();
		ClearAllTileVisuals();
		_perfWarned = false;
		var bootstrapMessage = TryBootstrapStarterTile();
		SetStatus(string.IsNullOrWhiteSpace(bootstrapMessage)
			? $"Started new session with seed {seed}."
			: $"Started new session with seed {seed}. {bootstrapMessage}");
		Render();
	}

	private string TryBootstrapStarterTile()
	{
		if (!_minimalPlayMode || _session is null)
			return null;

		try
		{
			if (_session.State.Board.Count > 0)
				return null;

			if (_session.State.CurrentTile is null)
				_session.IssueCommand(new DrawTileCommand());

			if (_session.State.CurrentTile is null)
				return "Starter draw unavailable.";

			var centerSlot = (GameState.BoardWidth * GameState.BoardHeight) / 2;
			_session.IssueCommand(new PlaceTileCommand(centerSlot, 0));
			_rotationQuarterTurns = 0;
			return $"Starter tile placed in slot {centerSlot}.";
		}
		catch
		{
			return "Starter tile bootstrap skipped (manual start).";
		}
	}

	private void CreateNewGameFromSeed()
	{
		if (!ulong.TryParse(_seedEdit.Text, out var seed))
		{
			SetStatus("Invalid seed.");
			return;
		}

		StartNewSession(seed);
	}

	private void SaveGame()
	{
		try
		{
			_saveRepository.Save(ResolveSavePath(), _session.State);
			SetStatus("Saved game state.");
		}
		catch (Exception ex)
		{
			SetStatus($"Save failed: {ex.Message}");
		}

		Render();
	}

	private void LoadGame()
	{
		if (_saveRepository.TryLoad(ResolveSavePath(), out var loaded, out var error))
		{
			_eventSink.Clear();
			_session = new GameSession(loaded, _eventSink, _highScoreRepository, ResolveHighScorePath());
			_renderedTiles.Clear();
			ClearAllTileVisuals();
			_perfWarned = false;
			_hoveredSlotIndex = null;
			AnimateHoverRing(false);
			SetStatus("Loaded game state.");
		}
		else
		{
			SetStatus($"Load failed: {error}");
		}

		RefreshHighScoreLabel();
		Render();
	}

	private void RefreshHighScoreLabel()
	{
		if (_highScoreRepository.TryLoadHighScore(ResolveHighScorePath(), out var highScore, out var error))
		{
			_highScoreLabel.Text = $"High Score: {highScore}";
			if (!string.IsNullOrWhiteSpace(error))
				SetStatus(error);
		}
		else
		{
			_highScoreLabel.Text = "High Score: (error)";
			SetStatus(error);
		}
	}

	private void Issue(IGameCommand command)
	{
		try
		{
			_session.IssueCommand(command);
			if (command is PlaceTileCommand placed)
				TriggerPlacementFocus(placed.SlotIndex);
			RefreshHighScoreLabel();
		}
		catch (Exception ex)
		{
			if (command is PlaceTileCommand)
				TriggerInvalidPlacementFeedback();
			SetStatus($"Command failed: {ex.Message}");
		}

		Render();
	}

	private void TriggerInvalidPlacementFeedback()
	{
		_statusErrorFlashTimer = 0.35f;
	}

	private void TriggerPlacementFocus(int slotIndex)
	{
		_placementFocusSlot = slotIndex;
		_placementFocusTimer = 0.45f;
	}

	private void TryPlaceFromScreenPoint(Vector2 screenPosition)
	{
		var slot = RaycastSlot(screenPosition);
		if (slot is null)
			return;

		Issue(new PlaceTileCommand(slot.Value, _rotationQuarterTurns));
		GetViewport().SetInputAsHandled();
	}

	private void UpdateHoverSlot()
	{
		if (_orbitDragging)
			return;

		var nextHovered = RaycastSlot(GetViewport().GetMousePosition());
		if (_hoveredSlotIndex == nextHovered)
			return;

		_hoveredSlotIndex = nextHovered;
		AnimateHoverRing(nextHovered is not null);
		RenderBoard();
	}

	private void AnimateHoverRing(bool visible)
	{
		_hoverRingTween?.Kill();
		if (visible && _hoveredSlotIndex is int slot && _slots.TryGetValue(slot, out var slotVisual))
		{
			_hoverRingRoot.Position = slotVisual.Root.Position;
			_hoverRingRoot.Visible = true;
		}

		_hoverRingTween = CreateTween();
		_hoverRingTween.SetEase(Tween.EaseType.Out);
		_hoverRingTween.SetTrans(Tween.TransitionType.Quad);
		_hoverRingTween.TweenProperty(this, "HoverRingAlpha", visible ? 0.85f : 0.0f, visible ? 0.12f : 0.10f);
	}

	private int? RaycastSlot(Vector2 screenPosition)
	{
		if (_camera is null)
			return null;

		var from = _camera.ProjectRayOrigin(screenPosition);
		var to = from + (_camera.ProjectRayNormal(screenPosition) * 200.0f);
		var query = PhysicsRayQueryParameters3D.Create(from, to);
		var result = GetWorld3D().DirectSpaceState.IntersectRay(query);
		if (result.Count == 0)
			return null;

		if (!result.TryGetValue("collider", out var colliderObj) || colliderObj.VariantType != Variant.Type.Object)
			return null;

		if (colliderObj.AsGodotObject() is not Node colliderNode || !colliderNode.HasMeta("slot_index"))
			return null;

		return (int)colliderNode.GetMeta("slot_index");
	}

	private void Render()
	{
		RenderBoard();

		var state = _session.State;
		_coinsLabel.Text = $"Coins  {state.Coins}";
		_scoreLabel.Text = $"Score  {state.Score}";
		_currentTileLabel.Text = state.CurrentTile is null
			? "No tile in hand"
			: $"{state.CurrentTile}";
		_rotationLabel.Text = $"Rotation {_rotationQuarterTurns * 90}\u00B0  (Q/E)";
		_unlockedTilesLabel.Text = _minimalPlayMode
			? $"Unlocked {state.UnlockedTiles.Count} tiles"
			: $"Unlocked: {string.Join(", ", state.UnlockedTiles.OrderBy(t => t))}";
		_lastMessageLabel.Text = $"Last: {CompactText(state.LastMessage, 72)}";
		_eventLog.Text = string.Join('\n', (_minimalPlayMode ? _eventSink.RecentLines.TakeLast(4) : _eventSink.RecentLines).Select(l => $"\u2022 {CompactText(l, 64)}"));
		_tileSwatchColor.Color = GetTileSwatchColor(state.CurrentTile);
		_tutorialHintPanel.Visible = state.CurrentTile is null;
		_eventTitleLabel.Text = _utilityPanelVisible
			? $"Recent  ({_lastMeshInstanceCount} meshes / {_lastNodeCount} nodes)"
			: "Recent";
		UpdateActionButtonStates(state);
	}

	private void UpdateActionButtonStates(GameState state)
	{
		if (_drawButton is null)
			return;

		var hasTile = state.CurrentTile is not null;
		_drawButton.Disabled = hasTile;
		_submitScoreButton.Disabled = state.Score <= 0;

		_drawButton.Text = hasTile ? "Draw (hand full)" : "Draw";
		_submitScoreButton.Text = state.Score > 0 ? $"Submit Score ({state.Score})" : "Submit Score";

		_drawButton.Modulate = _drawButton.Disabled ? new Color(0.86f, 0.89f, 0.86f) : new Color(0.92f, 1.0f, 0.94f);
		_submitScoreButton.Modulate = _submitScoreButton.Disabled ? new Color(0.86f, 0.88f, 0.92f) : new Color(0.92f, 0.94f, 1.0f);
	}

	private void RenderBoard()
	{
		foreach (var (slotIndex, visual) in _slots)
		{
			var isHovered = _hoveredSlotIndex == slotIndex;
			var occupied = _session.State.Board.TryGetValue(slotIndex, out var placed);
			visual.BaseMaterial.AlbedoColor = occupied
				? new Color(0.66f, 0.79f, 0.74f)
				: new Color(0.76f, 0.87f, 0.90f);
			visual.BaseMaterial.EmissionEnabled = isHovered;
			visual.BaseMaterial.Emission = new Color(0.88f, 0.95f, 1.0f);
			visual.BaseMaterial.EmissionEnergyMultiplier = isHovered ? 0.10f : 0.0f;
			visual.Overlay.Visible = !occupied && _showEmptyOverlays;

			if (!occupied)
			{
				if (_renderedTiles.Remove(slotIndex))
				{
					RemoveRipplesForSlot(slotIndex);
					ClearTileHolder(visual.TileRoot);
				}
				visual.TileRoot.Position = new Vector3(0, visual.TileRootBaseY, 0);
				continue;
			}

			var normalizedRotation = ((placed.RotationQuarterTurns % 4) + 4) % 4;
			var signature = BuildVisualSignature(slotIndex, placed.TileType, normalizedRotation);
			if (_renderedTiles.TryGetValue(slotIndex, out var existing) && existing.Equals(signature))
				continue;

			_renderedTiles[slotIndex] = signature;
			RebuildPlacedTileVisual(slotIndex, visual.TileRoot, placed.TileType, normalizedRotation, animate: true);
		}

		if (_hoveredSlotIndex is int hovered && _slots.TryGetValue(hovered, out var hoveredVisual))
			_hoverRingRoot.Position = hoveredVisual.Root.Position;

		UpdatePreviewVisual();
	}

	private void UpdateIdleMotion()
	{
		foreach (var (slotIndex, visual) in _slots)
		{
			if (_session is null || !_session.State.Board.ContainsKey(slotIndex))
			{
				visual.TileRoot.Position = new Vector3(0, visual.TileRootBaseY, 0);
				continue;
			}

			var bob = Mathf.Sin((_time * 1.8f) + visual.IdlePhase) * 0.028f;
			visual.TileRoot.Position = new Vector3(0, visual.TileRootBaseY + bob, 0);
		}
	}

	private void UpdateHintArrowPulse()
	{
		if (_hintArrowLabel is null)
			return;

		var show = _session is not null && _session.State.CurrentTile is null;
		_hintArrowLabel.Visible = show;
		if (!show)
			return;

		var pulse = (Mathf.Sin(_time * 4.0f) + 1.0f) * 0.5f;
		_hintArrowLabel.Modulate = new Color(1f, 0.98f, 0.85f, 0.42f + (pulse * 0.45f));
		_hintArrowLabel.Position = _hintArrowBasePosition + new Vector2(0, Mathf.Sin(_time * 4.0f) * 4.0f);
	}

	private void UpdateStatusFeedback(float delta)
	{
		if (_statusLabel is null)
			return;

		if (_statusErrorFlashTimer <= 0f)
		{
			_statusLabel.Modulate = Colors.White;
			return;
		}

		_statusErrorFlashTimer = Mathf.Max(0f, _statusErrorFlashTimer - delta);
		var t = _statusErrorFlashTimer / 0.35f;
		var pulse = 0.55f + (Mathf.Sin(_time * 24f) * 0.20f);
		var blend = Mathf.Clamp(Mathf.Max(t, pulse * t), 0f, 1f);
		_statusLabel.Modulate = Colors.White.Lerp(new Color(1f, 0.68f, 0.68f), blend);
	}

	private void UpdatePreviewVisual()
	{
		if (_hoveredSlotIndex is null ||
			!_slots.TryGetValue(_hoveredSlotIndex.Value, out var slot) ||
			_session.State.CurrentTile is null ||
			_session.State.Board.ContainsKey(_hoveredSlotIndex.Value))
		{
			_previewRoot.Visible = false;
			return;
		}

		_previewRoot.Visible = true;
		_previewRoot.Position = slot.Root.Position + new Vector3(0, 0.12f, 0);

		if (!_hasPreviewVisual || _previewTileType != _session.State.CurrentTile)
		{
			if (_hasPreviewVisual)
				_previewVisual.QueueFree();

			_previewTileType = _session.State.CurrentTile;
			_previewVisual = CreateTileDiorama(_previewTileType.Value, _hoveredSlotIndex.Value, preview: true);
			_previewRoot.AddChild(_previewVisual);
			_hasPreviewVisual = true;
		}

		_previewVisual.Rotation = new Vector3(0, _rotationQuarterTurns * Mathf.Pi / 2.0f, 0);
	}

	private void RebuildPlacedTileVisual(int slotIndex, Node3D tileRoot, TileType tileType, int rotationQuarterTurns, bool animate)
	{
		RemoveRipplesForSlot(slotIndex);
		ClearTileHolder(tileRoot);
		var diorama = CreateTileDiorama(tileType, slotIndex, preview: false);
		diorama.Rotation = new Vector3(0, rotationQuarterTurns * Mathf.Pi / 2.0f, 0);
		tileRoot.AddChild(diorama);

		if (animate)
		{
			diorama.Scale = new Vector3(0.85f, 0.85f, 0.85f);
			var tween = CreateTween();
			tween.SetEase(Tween.EaseType.Out);
			tween.SetTrans(Tween.TransitionType.Back);
			tween.TweenProperty(diorama, "scale", Vector3.One, 0.14);
		}
	}

	private static void ClearTileHolder(Node3D tileRoot)
	{
		foreach (var child in tileRoot.GetChildren())
			child.QueueFree();
	}

	private void ClearAllTileVisuals()
	{
		foreach (var slot in _slots.Values)
			ClearTileHolder(slot.TileRoot);
		_slotRipples.Clear();

		if (_hasPreviewVisual)
		{
			_previewVisual.QueueFree();
			_previewVisual = null!;
			_hasPreviewVisual = false;
			_previewTileType = null;
		}
	}

	private Node3D CreateTileDiorama(TileType tileType, int visualSeed, bool preview)
	{
		var root = new Node3D { Name = $"{tileType}Diorama" };
		var neighbors = GetNeighborTypes(visualSeed);

		root.AddChild(CreateTerrainSlab(tileType, visualSeed, preview));
		AddSeamSkirt(root, tileType, visualSeed, neighbors, preview);

		switch (tileType)
		{
			case TileType.Plains:
				BuildPlains(root, visualSeed, neighbors, preview);
				break;
			case TileType.Woods:
				BuildWoods(root, visualSeed, neighbors, preview);
				break;
			case TileType.River:
				BuildRiver(root, visualSeed, neighbors, preview);
				break;
			case TileType.Meadow:
				BuildMeadow(root, visualSeed, neighbors, preview);
				break;
			case TileType.Village:
				BuildVillage(root, visualSeed, neighbors, preview);
				break;
			case TileType.Lake:
				BuildLake(root, visualSeed, neighbors, preview);
				break;
		}

		return root;
	}

	private void BuildPlains(Node3D root, int seed, NeighborTypes neighbors, bool preview)
	{
		var rng = MakeVisualRng(seed, 1000);
		var composition = GetTileComposition(TileType.Plains, seed, neighbors);
		var microLumpCount = 3 + composition.DetailBias;
		AddMicroLumps(root, seed, preview, Mathf.Clamp(microLumpCount, 2, 6), new Color(0.34f, 0.43f, 0.26f));
		var dirtZone = GetBiomeZoneCenter(seed, 1025, composition, preferEdge: composition.Dryness > 0.50f);
		var grassZone = GetBiomeZoneCenter(seed, 1035, composition, preferEdge: false);

		var dirtPatchCount = composition.PatchDensity >= 0.58f ? 2 : 1;
		for (var i = 0; i < dirtPatchCount; i++)
		{
			var pos = RandomPointNear(dirtZone, rng, 0.16f, Mathf.Lerp(0.46f, 0.62f, composition.PatchSpread));
			var patch = CreateColoredMesh(
				new BoxMesh
				{
					Size = new Vector3(
						rng.RandfRange(0.20f, 0.34f) * composition.PatchDensityScale,
						0.02f,
						rng.RandfRange(0.12f, 0.22f) * Mathf.Lerp(0.9f, 1.12f, composition.PatchSpread))
				},
				pos + new Vector3(0, 0.205f, 0),
				new Color(0.29f, 0.32f, 0.21f).Lerp(new Color(0.33f, 0.35f, 0.23f), composition.Dryness),
				preview);
			patch.Rotation = new Vector3(0, rng.RandfRange(0f, Mathf.Tau), 0);
			if (patch.MaterialOverride is StandardMaterial3D patchMat)
				ApplyMaterialJitter(patchMat, seed, 1100 + i, 0.05f, 0.92f);
			root.AddChild(patch);
		}

		var count = rng.RandiRange(1 + Math.Max(0, composition.DetailBias / 2), 2 + Math.Max(0, composition.DetailBias));
		for (var i = 0; i < count; i++)
		{
			var pos = RandomPointNear(grassZone, rng, 0.22f, Mathf.Lerp(0.50f, 0.68f, composition.PatchSpread));
			for (var q = 0; q < 2; q++)
			{
				var blade = CreateColoredMesh(
					new PlaneMesh { Size = new Vector2(0.14f, rng.RandfRange(0.18f, 0.36f) * composition.DetailScale) },
					pos + new Vector3(0, 0.21f + rng.RandfRange(-0.01f, 0.05f), 0),
					new Color(0.30f, 0.50f, 0.22f).Lerp(new Color(0.28f, 0.46f, 0.20f), composition.Dryness),
					preview);
				blade.Rotation = new Vector3(0, rng.RandfRange(0, Mathf.Tau) + (q * Mathf.Pi * 0.5f), rng.RandfRange(-0.06f, 0.06f));
				if (blade.MaterialOverride is StandardMaterial3D mat)
				{
					mat.CullMode = BaseMaterial3D.CullModeEnum.Disabled;
					ApplyMaterialJitter(mat, seed, 1200 + i * 3 + q, 0.08f, 0.94f);
				}
				root.AddChild(blade);
			}
		}
	}

	private void BuildWoods(Node3D root, int seed, NeighborTypes neighbors, bool preview)
	{
		var rng = MakeVisualRng(seed, 2000);
		var composition = GetTileComposition(TileType.Woods, seed, neighbors);
		var woodlandZone = GetBiomeZoneCenter(seed, 2025, composition, preferEdge: composition.PatchSpread > 0.58f);
		var minTrees = composition.PatchDensity >= 0.58f ? 3 : 2;
		var maxTrees = composition.PatchDensity >= 0.72f ? 4 : 3;
		var treeCount = rng.RandiRange(minTrees, maxTrees);
		for (var i = 0; i < treeCount; i++)
		{
			var point = RandomPointNear(woodlandZone, rng, 0.24f, Mathf.Lerp(0.44f, 0.60f, composition.PatchSpread));
			var trunkHeight = rng.RandfRange(0.18f, 0.28f);
			var trunk = CreateColoredMesh(
				new CylinderMesh
				{
					Height = trunkHeight,
					TopRadius = 0.025f,
					BottomRadius = 0.045f,
					RadialSegments = 8
				},
				point + new Vector3(0, 0.16f + trunkHeight * 0.5f, 0),
				new Color(0.46f, 0.33f, 0.22f),
				preview);
			if (trunk.MaterialOverride is StandardMaterial3D trunkMat)
				ApplyMaterialJitter(trunkMat, seed, 2100 + i, 0.06f, 0.96f);
			root.AddChild(trunk);

			Mesh canopyMesh = rng.Randf() < 0.45f
				? new CylinderMesh
				{
					Height = rng.RandfRange(0.24f, 0.36f),
					TopRadius = 0.0f,
					BottomRadius = rng.RandfRange(0.14f, 0.20f),
					RadialSegments = 8
				}
				: new SphereMesh
				{
					Radius = rng.RandfRange(0.16f, 0.22f),
					Height = rng.RandfRange(0.28f, 0.36f),
					RadialSegments = 10,
					Rings = 6
				};

			var canopy = CreateColoredMesh(
				canopyMesh,
				point + new Vector3(0, 0.34f + trunkHeight, 0),
				new Color(0.15f, 0.36f, 0.16f),
				preview);
			canopy.Rotation = new Vector3(
				rng.RandfRange(-0.15f, 0.15f),
				rng.RandfRange(0f, Mathf.Tau),
				rng.RandfRange(-0.15f, 0.15f));
			if (canopy.MaterialOverride is StandardMaterial3D canopyMat)
				ApplyMaterialJitter(canopyMat, seed, 2200 + i, 0.08f, 0.92f);
			root.AddChild(canopy);
		}

		if (!preview && rng.Randf() < 0.7f)
		{
			var logPos = RandomPointNear(woodlandZone * 0.75f, rng, 0.20f, 0.48f);
			var log = CreateColoredMesh(
				new CylinderMesh { Height = 0.34f, TopRadius = 0.045f, BottomRadius = 0.05f, RadialSegments = 8 },
				logPos + new Vector3(0, 0.18f, 0),
				new Color(0.38f, 0.28f, 0.18f),
				preview);
			log.Rotation = new Vector3(Mathf.Pi * 0.5f, rng.RandfRange(0f, Mathf.Tau), rng.RandfRange(-0.2f, 0.2f));
			if (log.MaterialOverride is StandardMaterial3D logMat)
				ApplyMaterialJitter(logMat, seed, 2300, 0.06f, 0.95f);
			root.AddChild(log);
		}

		foreach (var dir in CardinalDirs())
		{
			var neighbor = neighbors[dir];
			if (neighbor is TileType.Plains or TileType.Meadow)
				AddEdgeFeatherTufts(root, seed, dir, preview, 2, new Color(0.24f, 0.42f, 0.20f));
		}
	}

	private void BuildRiver(Node3D root, int seed, NeighborTypes neighbors, bool preview)
	{
		var rng = MakeVisualRng(seed, 3000);
		var composition = GetTileComposition(TileType.River, seed, neighbors);
		BuildConnectedRiverWater(root, seed, neighbors, preview, composition);
		AddWaterTileLandPatches(root, seed, preview, composition, isLake: false);

		var shoreRockCount = composition.WaterDominance >= 0.62f ? 1 : 2;
		for (var i = 0; i < shoreRockCount; i++)
		{
			var rock = CreateColoredMesh(
				new SphereMesh { Radius = 0.05f, Height = 0.06f, RadialSegments = 8, Rings = 5 },
				RandomTopPoint(rng, 0.58f) + new Vector3(0, 0.19f, 0),
				new Color(0.46f, 0.47f, 0.45f),
				preview);
			if (rock.MaterialOverride is StandardMaterial3D rockMat)
				ApplyMaterialJitter(rockMat, seed, 3020 + i, 0.05f, 0.95f);
			root.AddChild(rock);
		}

		foreach (var dir in CardinalDirs())
		{
			if (!IsWater(neighbors[dir]))
				AddWaterEdgeBank(root, seed, dir, preview);
		}
	}

	private void BuildMeadow(Node3D root, int seed, NeighborTypes neighbors, bool preview)
	{
		var rng = MakeVisualRng(seed, 4000);
		var composition = GetTileComposition(TileType.Meadow, seed, neighbors);
		AddMicroLumps(root, seed, preview, Mathf.Clamp(2 + composition.DetailBias, 2, 5), new Color(0.37f, 0.46f, 0.28f));
		var grassZone = GetBiomeZoneCenter(seed, 4020, composition, preferEdge: true);
		var flowerZone = GetBiomeZoneCenter(seed, 4030, composition, preferEdge: composition.Dryness < 0.45f);

		var edgeGrassCount = composition.PatchDensity >= 0.6f ? 3 : 2;
		for (var i = 0; i < edgeGrassCount; i++)
		{
			var pos = RandomPointNear(grassZone, rng, 0.20f, Mathf.Lerp(0.46f, 0.64f, composition.PatchSpread));
			var blade = CreateColoredMesh(
				new PlaneMesh { Size = new Vector2(0.12f, rng.RandfRange(0.20f, 0.28f)) },
				pos + new Vector3(0, 0.22f + rng.RandfRange(-0.01f, 0.03f), 0),
				new Color(0.29f, 0.48f, 0.22f),
				preview);
			blade.Rotation = new Vector3(0, rng.RandfRange(0f, Mathf.Tau), 0);
			if (blade.MaterialOverride is StandardMaterial3D bladeMat)
			{
				bladeMat.CullMode = BaseMaterial3D.CullModeEnum.Disabled;
				ApplyMaterialJitter(bladeMat, seed, 4050 + i, 0.07f, 0.95f);
			}
			root.AddChild(blade);
		}

		var palette = new[]
		{
			new Color(0.74f, 0.66f, 0.24f),
			new Color(0.74f, 0.34f, 0.30f),
			new Color(0.88f, 0.86f, 0.78f)
		};
		var flowerMax = composition.Dryness > 0.45f ? 4 : 6;
		for (var i = 0; i < rng.RandiRange(3, flowerMax); i++)
		{
			var pos = RandomPointNear(flowerZone, rng, 0.22f, Mathf.Lerp(0.44f, 0.60f, composition.PatchSpread));
			var color = palette[rng.RandiRange(0, palette.Length - 1)];
			var flower = CreateColoredMesh(
				new SphereMesh { Radius = 0.025f, Height = 0.05f, RadialSegments = 8, Rings = 5 },
				pos + new Vector3(0, 0.23f + rng.RandfRange(-0.01f, 0.02f), 0),
				color,
				preview);
			if (flower.MaterialOverride is StandardMaterial3D flowerMat)
				ApplyMaterialJitter(flowerMat, seed, 4100 + i, 0.06f, 0.98f);
			root.AddChild(flower);
		}

		for (var i = 0; i < (composition.DetailBias > 0 ? 2 : 1); i++)
		{
			var pos = RandomPointNear(grassZone, rng, 0.18f, Mathf.Lerp(0.42f, 0.58f, composition.PatchSpread));
			var tuft = CreateColoredMesh(
				new PlaneMesh { Size = new Vector2(0.10f, rng.RandfRange(0.26f, 0.38f)) },
				pos + new Vector3(0, 0.23f, 0),
				new Color(0.28f, 0.48f, 0.21f),
				preview);
			tuft.Rotation = new Vector3(0, rng.RandfRange(0f, Mathf.Tau), rng.RandfRange(-0.05f, 0.05f));
			if (tuft.MaterialOverride is StandardMaterial3D tuftMat)
			{
				tuftMat.CullMode = BaseMaterial3D.CullModeEnum.Disabled;
				ApplyMaterialJitter(tuftMat, seed, 4200 + i, 0.08f, 0.94f);
			}
			root.AddChild(tuft);
		}

		foreach (var dir in CardinalDirs())
		{
			if (neighbors[dir] == TileType.Plains)
				AddEdgeFeatherTufts(root, seed + 33, dir, preview, 1, new Color(0.26f, 0.44f, 0.21f));
		}
	}

	private void BuildVillage(Node3D root, int seed, NeighborTypes neighbors, bool preview)
	{
		var rng = MakeVisualRng(seed, 5000);
		var composition = GetTileComposition(TileType.Village, seed, neighbors);
		var road = CreateColoredMesh(
			new BoxMesh { Size = new Vector3(0.22f, 0.015f, 0.88f) },
			new Vector3(0, 0.205f, 0),
			new Color(0.36f, 0.33f, 0.29f),
			preview);
		road.Rotation = new Vector3(0, (rng.RandiRange(0, 1) * Mathf.Pi * 0.5f), 0);
		if (road.MaterialOverride is StandardMaterial3D roadMat)
			ApplyMaterialJitter(roadMat, seed, 5050, 0.04f, 0.94f);
		root.AddChild(road);

		var homeCount = composition.PatchDensity >= 0.62f ? 3 : 2;
		for (var i = 0; i < homeCount; i++)
		{
			var pos = RandomTopPoint(rng, Mathf.Lerp(0.34f, 0.46f, composition.PatchSpread));
			var wallSize = new Vector3(rng.RandfRange(0.18f, 0.26f), rng.RandfRange(0.12f, 0.18f), rng.RandfRange(0.17f, 0.25f));
			var walls = CreateColoredMesh(
				new BoxMesh { Size = wallSize },
				pos + new Vector3(0, 0.22f + wallSize.Y * 0.5f, 0),
				new Color(0.79f, 0.75f, 0.68f),
				preview);
			if (walls.MaterialOverride is StandardMaterial3D wallMat)
				ApplyMaterialJitter(wallMat, seed, 5100 + i, 0.07f, 0.96f);
			root.AddChild(walls);

			var roof = CreateColoredMesh(
				new CylinderMesh
				{
					Height = 0.08f,
					TopRadius = 0.0f,
					BottomRadius = Mathf.Max(wallSize.X, wallSize.Z) * 0.72f,
					RadialSegments = 4
				},
				pos + new Vector3(0, 0.28f + wallSize.Y, 0),
				new Color(0.42f, 0.31f, 0.28f),
				preview);
			roof.Rotation = new Vector3(0, (Mathf.Pi * 0.5f) * rng.RandiRange(0, 3), 0);
			if (roof.MaterialOverride is StandardMaterial3D roofMat)
			{
				roofMat.Metallic = 0.08f;
				roofMat.Roughness = 0.42f + Mathf.Abs(DeterministicJitter(seed, 5200 + i)) * 0.08f;
			}
			root.AddChild(roof);
		}
	}

	private void BuildLake(Node3D root, int seed, NeighborTypes neighbors, bool preview)
	{
		var rng = MakeVisualRng(seed, 6000);
		var composition = GetTileComposition(TileType.Lake, seed, neighbors);
		var lakeRadius = Mathf.Lerp(0.36f, 0.62f, composition.WaterDominance);
		BuildWaterLayers(root, seed, preview, isLake: true, rng.RandfRange(-0.1f, 0.1f), new Vector3(lakeRadius, 0.14f, lakeRadius));
		AddWaterTileLandPatches(root, seed, preview, composition, isLake: true);

		var shoreClusterCount = composition.WaterDominance >= 0.7f ? 2 : 3;
		for (var i = 0; i < shoreClusterCount; i++)
		{
			var rock = CreateColoredMesh(
				new SphereMesh { Radius = 0.05f, Height = 0.06f, RadialSegments = 8, Rings = 5 },
				RandomTopPoint(rng, 0.60f) + new Vector3(0, 0.19f, 0),
				new Color(0.48f, 0.49f, 0.47f),
				preview);
			root.AddChild(rock);
		}

		foreach (var dir in CardinalDirs())
		{
			if (!IsWater(neighbors[dir]))
				AddWaterEdgeBank(root, seed + 77, dir, preview);
		}
	}

	private void BuildWaterLayers(Node3D root, int seed, bool preview, bool isLake, float yaw, Vector3 sizeHint)
	{
		var y = isLake ? 0.135f : 0.130f;
		Mesh baseMesh = isLake
			? new CylinderMesh { Height = 0.020f, TopRadius = sizeHint.X, BottomRadius = sizeHint.X, RadialSegments = 20 }
			: new BoxMesh { Size = new Vector3(sizeHint.X, 0.018f, sizeHint.Z) };
		Mesh highlightMesh = isLake
			? new CylinderMesh { Height = 0.010f, TopRadius = sizeHint.X * 0.78f, BottomRadius = sizeHint.X * 0.78f, RadialSegments = 20 }
			: new BoxMesh { Size = new Vector3(sizeHint.X * 0.72f, 0.010f, sizeHint.Z * 0.80f) };

		var baseWater = CreateColoredMesh(baseMesh, new Vector3(0, y, 0), isLake ? new Color(0.14f, 0.43f, 0.52f) : new Color(0.16f, 0.46f, 0.60f), preview);
		baseWater.Rotation = new Vector3(0, yaw, 0);
		ConfigureWaterMaterial((StandardMaterial3D)baseWater.MaterialOverride, seed, isLake, preview, highlightLayer: false);
		baseWater.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
		root.AddChild(baseWater);

		var highlight = CreateColoredMesh(highlightMesh, new Vector3(0, y + 0.008f, 0), isLake ? new Color(0.24f, 0.66f, 0.76f) : new Color(0.22f, 0.62f, 0.78f), preview);
		highlight.Rotation = new Vector3(0, yaw + 0.03f, 0);
		ConfigureWaterMaterial((StandardMaterial3D)highlight.MaterialOverride, seed + 1, isLake, preview, highlightLayer: true);
		highlight.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
		root.AddChild(highlight);

		BuildWetShoreSegments(root, seed, preview, isLake, yaw, isLake ? 0.50f : 0.56f);
		if (!preview)
			BuildWaterRipples(root, seed, isLake, yaw, isLake ? 0.42f : 0.46f, y + 0.014f);
	}

	private void BuildConnectedRiverWater(Node3D root, int seed, NeighborTypes neighbors, bool preview, TileComposition composition)
	{
		var north = IsWater(neighbors.North);
		var east = IsWater(neighbors.East);
		var south = IsWater(neighbors.South);
		var west = IsWater(neighbors.West);

		var vertical = north || south;
		var horizontal = east || west;
		var connectionCount = new[] { north, east, south, west }.Count(x => x);

		var width = Mathf.Lerp(0.28f, 0.56f, composition.WaterDominance);
		var length = Mathf.Lerp(0.92f, 1.24f, composition.WaterDominance);

		// Main channel(s)
		if (vertical && horizontal)
		{
			BuildWaterLayers(root, seed, preview, isLake: false, 0f, new Vector3(width * 0.88f, 0.135f, length));
			BuildWaterLayers(root, seed + 1, preview, isLake: false, Mathf.Pi * 0.5f, new Vector3(width * 0.88f, 0.135f, length));
		}
		else if (horizontal)
		{
			BuildWaterLayers(root, seed, preview, isLake: false, Mathf.Pi * 0.5f, new Vector3(width, 0.135f, length));
		}
		else
		{
			BuildWaterLayers(root, seed, preview, isLake: false, 0f, new Vector3(width, 0.135f, length));
		}

		// Connection bridge strips toward matching neighbors.
		foreach (var dir in CardinalDirs())
		{
			if (!IsWater(neighbors[dir]))
				continue;

			var vec = DirectionVector(dir);
			var bridge = CreateColoredMesh(
				new BoxMesh { Size = new Vector3(Mathf.Lerp(0.26f, 0.40f, composition.WaterDominance), 0.016f, 0.44f) },
				new Vector3(vec.X * 0.55f, 0.132f, vec.Z * 0.55f),
				new Color(0.20f, 0.58f, 0.74f),
				preview);
			bridge.Rotation = new Vector3(0, DirectionYaw(dir), 0);
			if (bridge.MaterialOverride is StandardMaterial3D bridgeMat)
				ConfigureWaterMaterial(bridgeMat, seed + 500 + (int)dir, isLake: false, preview, highlightLayer: false);
			bridge.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
			root.AddChild(bridge);
		}

		if (connectionCount == 0)
		{
			// Isolated river tile still gets a believable orientation, deterministic by slot seed.
			var yaw = (Mathf.Abs(DeterministicJitter(seed, 3550)) > 0.2f) ? 0f : Mathf.Pi * 0.5f;
			BuildWaterLayers(root, seed + 9, preview, isLake: false, yaw, new Vector3(width * 0.92f, 0.135f, length * 0.9f));
		}
	}

	private void AddWaterTileLandPatches(Node3D root, int seed, bool preview, TileComposition composition, bool isLake)
	{
		var rng = MakeVisualRng(seed, isLake ? 7350 : 4350);
		var landPresence = Mathf.Clamp(1.0f - composition.WaterDominance, 0.08f, 0.82f);
		var patchCount = landPresence switch
		{
			< 0.20f => 1,
			< 0.40f => 2,
			< 0.65f => 3,
			_ => 4
		};

		var radialMax = isLake
			? Mathf.Lerp(0.28f, 0.62f, landPresence)
			: Mathf.Lerp(0.40f, 0.68f, landPresence);

		var patchColorA = isLake
			? new Color(0.33f, 0.41f, 0.30f)
			: new Color(0.31f, 0.39f, 0.28f);
		var patchColorB = isLake
			? new Color(0.28f, 0.35f, 0.26f)
			: new Color(0.26f, 0.34f, 0.24f);

		for (var i = 0; i < patchCount; i++)
		{
			var pos = RandomTopPoint(rng, radialMax);
			if (!isLake)
			{
				// For rivers, keep banks away from the center line so the water channel stays legible.
				var centerAvoid = 0.18f + (composition.WaterDominance * 0.12f);
				if (Mathf.Abs(pos.X) < centerAvoid)
					pos = new Vector3(Mathf.Sign(pos.X == 0 ? (rng.Randf() - 0.5f) : pos.X) * centerAvoid, 0, pos.Z);
			}

			var patchSize = new Vector3(
				rng.RandfRange(0.12f, 0.26f) * Mathf.Lerp(0.85f, 1.25f, landPresence),
				rng.RandfRange(0.010f, 0.020f),
				rng.RandfRange(0.08f, 0.20f) * Mathf.Lerp(0.85f, 1.30f, landPresence));

			var patch = CreateColoredMesh(
				new BoxMesh { Size = patchSize },
				pos + new Vector3(0, 0.196f + rng.RandfRange(-0.005f, 0.01f), 0),
				patchColorA.Lerp(patchColorB, rng.Randf()),
				preview);
			patch.Rotation = new Vector3(0, rng.RandfRange(0f, Mathf.Tau), rng.RandfRange(-0.03f, 0.03f));
			if (patch.MaterialOverride is StandardMaterial3D patchMat)
				ApplyMaterialJitter(patchMat, seed, (isLake ? 7420 : 4420) + i, 0.05f, 0.92f);
			root.AddChild(patch);

			var tuftChance = Mathf.Lerp(0.20f, 0.70f, landPresence);
			if (rng.Randf() <= tuftChance)
			{
				var tuft = CreateColoredMesh(
					new PlaneMesh { Size = new Vector2(0.10f, rng.RandfRange(0.14f, 0.24f)) },
					pos + new Vector3(rng.RandfRange(-0.04f, 0.04f), 0.22f, rng.RandfRange(-0.04f, 0.04f)),
					new Color(0.24f, 0.40f, 0.20f),
					preview);
				tuft.Rotation = new Vector3(0, rng.RandfRange(0f, Mathf.Tau), rng.RandfRange(-0.06f, 0.06f));
				if (tuft.MaterialOverride is StandardMaterial3D tuftMat)
				{
					tuftMat.CullMode = BaseMaterial3D.CullModeEnum.Disabled;
					ApplyMaterialJitter(tuftMat, seed, (isLake ? 7480 : 4480) + i, 0.08f, 0.94f);
				}
				root.AddChild(tuft);
			}
		}
	}

	private void AddSeamSkirt(Node3D root, TileType tileType, int seed, NeighborTypes neighbors, bool preview)
	{
		var skirtRoot = new Node3D { Name = "SeamSkirt", Position = new Vector3(0, 0.11f, 0) };
		root.AddChild(skirtRoot);

		var skirtColor = tileType switch
		{
			TileType.Woods => new Color(0.20f, 0.24f, 0.16f),
			TileType.River or TileType.Lake => new Color(0.26f, 0.28f, 0.24f),
			TileType.Village => new Color(0.36f, 0.32f, 0.28f),
			_ => new Color(0.28f, 0.33f, 0.22f)
		};
		var skirtMat = CreateColorMaterial(skirtColor, preview);
		skirtMat.Roughness = 0.95f;
		ApplyMaterialJitter(skirtMat, seed, 8800, 0.04f, 0.94f);

		const int segments = 10;
		var radius = HexRadius * 0.84f;
		for (var i = 0; i < segments; i++)
		{
			var angle = Mathf.Tau * i / segments;
			var dir = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle));

			var sharedEdge = (neighbors.North is not null && Dot3(dir, DirectionVector(NeighborDir.North)) > 0.82f) ||
							 (neighbors.East is not null && Dot3(dir, DirectionVector(NeighborDir.East)) > 0.82f) ||
							 (neighbors.South is not null && Dot3(dir, DirectionVector(NeighborDir.South)) > 0.82f) ||
							 (neighbors.West is not null && Dot3(dir, DirectionVector(NeighborDir.West)) > 0.82f);
			if (sharedEdge)
				continue;

			var seg = new MeshInstance3D
			{
				Mesh = new BoxMesh { Size = new Vector3(0.26f, 0.06f, 0.05f) },
				Position = new Vector3(dir.X * radius, 0, dir.Z * radius),
				Rotation = new Vector3(0, -angle, 0),
				MaterialOverride = skirtMat,
				CastShadow = GeometryInstance3D.ShadowCastingSetting.On
			};
			skirtRoot.AddChild(seg);
		}
	}

	private void AddEdgeFeatherTufts(Node3D root, int seed, NeighborDir dir, bool preview, int count, Color color)
	{
		var rng = MakeVisualRng(seed, 9600 + (int)dir);
		var edgeVec = DirectionVector(dir);
		var tangent = new Vector3(-edgeVec.Z, 0, edgeVec.X);
		for (var i = 0; i < count; i++)
		{
			var offset = rng.RandfRange(-0.22f, 0.22f);
			var pos = (edgeVec * 0.55f) + (tangent * offset);
			var tuft = CreateColoredMesh(
				new PlaneMesh { Size = new Vector2(0.10f, rng.RandfRange(0.16f, 0.24f)) },
				pos + new Vector3(0, 0.22f, 0),
				color,
				preview);
			tuft.Rotation = new Vector3(0, DirectionYaw(dir) + rng.RandfRange(-0.4f, 0.4f), 0);
			if (tuft.MaterialOverride is StandardMaterial3D mat)
			{
				mat.CullMode = BaseMaterial3D.CullModeEnum.Disabled;
				ApplyMaterialJitter(mat, seed, 9650 + i + (int)dir * 10, 0.07f, 0.95f);
			}
			root.AddChild(tuft);
		}
	}

	private void AddWaterEdgeBank(Node3D root, int seed, NeighborDir dir, bool preview)
	{
		var rng = MakeVisualRng(seed, 9800 + (int)dir);
		var edgeVec = DirectionVector(dir);
		var tangent = new Vector3(-edgeVec.Z, 0, edgeVec.X);

		// Wet band
		var wetBand = CreateColoredMesh(
			new BoxMesh { Size = new Vector3(0.34f, 0.01f, 0.07f) },
			edgeVec * 0.54f + new Vector3(0, 0.145f, 0),
			new Color(0.22f, 0.28f, 0.22f),
			preview);
		wetBand.Rotation = new Vector3(0, DirectionYaw(dir), 0);
		root.AddChild(wetBand);

		// Shore stones
		var rockCount = rng.RandiRange(1, 2);
		for (var i = 0; i < rockCount; i++)
		{
			var pos = (edgeVec * rng.RandfRange(0.46f, 0.62f)) + (tangent * rng.RandfRange(-0.14f, 0.14f));
			var rock = CreateColoredMesh(
				new SphereMesh { Radius = rng.RandfRange(0.035f, 0.05f), Height = 0.05f, RadialSegments = 8, Rings = 5 },
				pos + new Vector3(0, 0.18f, 0),
				new Color(0.45f, 0.46f, 0.44f),
				preview);
			root.AddChild(rock);
		}
	}

	private static bool IsWater(TileType? tileType) => tileType is TileType.River or TileType.Lake;

	private static float Dot3(Vector3 a, Vector3 b) => (a.X * b.X) + (a.Y * b.Y) + (a.Z * b.Z);

	private MeshInstance3D CreateTerrainSlab(TileType tileType, int visualSeed, bool preview)
	{
		var mesh = BuildTerrainHexMesh(tileType, visualSeed);
		var composition = GetTileComposition(tileType, visualSeed, default);
		var instance = new MeshInstance3D
		{
			Mesh = mesh,
			Position = new Vector3(0, 0.02f, 0),
			CastShadow = GeometryInstance3D.ShadowCastingSetting.On
		};

		var baseColor = tileType switch
		{
			TileType.Plains => new Color(0.40f, 0.50f, 0.28f),
			TileType.Woods => new Color(0.28f, 0.32f, 0.22f),
			TileType.River => new Color(0.36f, 0.39f, 0.31f),
			TileType.Meadow => new Color(0.45f, 0.56f, 0.31f),
			TileType.Village => new Color(0.49f, 0.44f, 0.36f),
			TileType.Lake => new Color(0.35f, 0.40f, 0.35f),
			_ => new Color(0.5f, 0.5f, 0.5f)
		};

		var tint = 0.96f + (Mathf.Abs(Mathf.Sin(visualSeed * 0.173f)) * 0.08f);
		var patchDarken = 1.0f - Mathf.Clamp(Mathf.Abs(_terrainNoise.GetNoise2D(visualSeed * 0.11f, 3.17f)) * 0.08f, 0f, 0.08f);
		if (tileType is TileType.River or TileType.Lake)
			patchDarken -= Mathf.Lerp(0.02f, 0.07f, composition.WaterDominance);
		var roughnessBase = tileType switch
		{
			TileType.River or TileType.Lake => 0.82f,
			TileType.Village => 0.74f,
			_ => 0.90f
		};
		var mat = new StandardMaterial3D
		{
			AlbedoColor = preview
				? new Color(baseColor.R * tint * patchDarken, baseColor.G * tint * patchDarken, baseColor.B * tint * patchDarken, 0.45f)
				: new Color(baseColor.R * tint * patchDarken, baseColor.G * tint * patchDarken, baseColor.B * tint * patchDarken),
			Metallic = 0.02f,
			Roughness = roughnessBase
		};
		ApplyMaterialJitter(mat, visualSeed, 7000 + (int)tileType, 0.06f, 1.0f);
		if (preview)
			mat.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
		instance.MaterialOverride = mat;
		return instance;
	}

	private ArrayMesh BuildTerrainHexMesh(TileType tileType, int visualSeed)
	{
		var surface = new SurfaceTool();
		surface.Begin(Mesh.PrimitiveType.Triangles);

		var topRadius = HexRadius * 0.80f;
		var bevelRadius = HexRadius * 0.78f;
		var bottomRadius = HexRadius * 0.74f;
		const float topY = 0.22f;
		const float bevelY = 0.15f;
		const float bottomY = 0.0f;

		var centerTop = new Vector3(0, topY + SampleTerrainOffset(tileType, visualSeed, Vector2.Zero), 0);
		var centerBottom = new Vector3(0, bottomY, 0);
		var top = new Vector3[6];
		var bevel = new Vector3[6];
		var bottom = new Vector3[6];

		for (var i = 0; i < 6; i++)
		{
			var angle = Mathf.Pi / 6f + (Mathf.Tau * i / 6f);
			var dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
			top[i] = new Vector3(dir.X * topRadius, topY + SampleTerrainOffset(tileType, visualSeed, dir * 0.95f), dir.Y * topRadius);
			var erosion = Mathf.Clamp(_terrainNoise.GetNoise2D(dir.X * 7f + visualSeed, dir.Y * 7f - visualSeed) * 0.02f, -0.018f, 0.028f);
			bevel[i] = new Vector3(dir.X * bevelRadius, bevelY + (SampleTerrainOffset(tileType, visualSeed, dir * 0.65f) * 0.45f) + erosion, dir.Y * bevelRadius);
			bottom[i] = new Vector3(dir.X * bottomRadius, bottomY, dir.Y * bottomRadius);
		}

		for (var i = 0; i < 6; i++)
		{
			var n = (i + 1) % 6;
			AddTriangle(surface, centerTop, top[i], top[n]);
		}

		for (var i = 0; i < 6; i++)
		{
			var n = (i + 1) % 6;
			AddQuad(surface, top[i], top[n], bevel[n], bevel[i]);
			AddQuad(surface, bevel[i], bevel[n], bottom[n], bottom[i]);
		}

		for (var i = 0; i < 6; i++)
		{
			var n = (i + 1) % 6;
			AddTriangle(surface, centerBottom, bottom[n], bottom[i]);
		}

		surface.GenerateNormals();
		return surface.Commit();
	}

	private float SampleTerrainOffset(TileType tileType, int visualSeed, Vector2 point)
	{
		var composition = GetTileComposition(tileType, visualSeed, default);
		_terrainNoise.Seed = visualSeed * 97 + 13;
		var noise = _terrainNoise.GetNoise2D(point.X + 10.7f, point.Y - 3.1f);
		var amplitude = tileType switch
		{
			TileType.Plains => 0.06f,
			TileType.Woods => 0.09f,
			TileType.River => 0.07f,
			TileType.Meadow => 0.07f,
			TileType.Village => 0.05f,
			TileType.Lake => 0.08f,
			_ => 0.06f
		};

		var result = noise * amplitude * composition.HeightScale;
		var radius = point.Length();

		if (tileType == TileType.River)
		{
			var axisSkew = Mathf.Lerp(0.18f, 0.52f, composition.PatchSpread);
			var axis = point.X * (0.94f - axisSkew * 0.25f) + point.Y * axisSkew;
			var radialFalloff = Mathf.Exp(-(point.LengthSquared()) * 1.8f);
			var depth = Mathf.Lerp(0.05f, 0.12f, composition.WaterDominance);
			result -= Mathf.Exp(-(axis * axis) * Mathf.Lerp(5.5f, 8.0f, composition.WaterDominance)) * ((depth * radialFalloff) + 0.01f);
		}
		else if (tileType == TileType.Lake)
		{
			result -= Mathf.Exp(-(radius * radius) * Mathf.Lerp(4.4f, 6.6f, composition.WaterDominance)) * Mathf.Lerp(0.08f, 0.14f, composition.WaterDominance);
		}
		else if (tileType == TileType.Village)
		{
			result *= 1.0f - Mathf.Exp(-(radius * radius) * 5.8f) * 0.85f;
		}

		return result;
	}

	private static void AddTriangle(SurfaceTool surface, Vector3 a, Vector3 b, Vector3 c)
	{
		surface.AddVertex(a);
		surface.AddVertex(b);
		surface.AddVertex(c);
	}

	private static void AddQuad(SurfaceTool surface, Vector3 a, Vector3 b, Vector3 c, Vector3 d)
	{
		AddTriangle(surface, a, b, c);
		AddTriangle(surface, a, c, d);
	}

	private static RandomNumberGenerator MakeVisualRng(int seed, int salt)
	{
		var mixed = (uint)(seed * 73856093) ^ (uint)(salt * 19349663);
		return new RandomNumberGenerator { Seed = mixed };
	}

	private static Vector3 RandomTopPoint(RandomNumberGenerator rng, float maxRadius)
	{
		var angle = rng.RandfRange(0f, Mathf.Tau);
		var r = rng.RandfRange(0.06f, maxRadius);
		return new Vector3(Mathf.Cos(angle) * r, 0, Mathf.Sin(angle) * r);
	}

	private static Vector3 RandomPointNear(Vector3 center, RandomNumberGenerator rng, float spread, float clampRadius)
	{
		var angle = rng.RandfRange(0f, Mathf.Tau);
		var r = rng.RandfRange(0.02f, spread);
		var point = center + new Vector3(Mathf.Cos(angle) * r, 0, Mathf.Sin(angle) * r);
		var v = new Vector2(point.X, point.Z);
		var len = v.Length();
		if (len > clampRadius && len > 0.0001f)
		{
			v = v.Normalized() * clampRadius;
			point = new Vector3(v.X, 0, v.Y);
		}

		return point;
	}

	private Vector3 GetBiomeZoneCenter(int seed, int salt, TileComposition composition, bool preferEdge)
	{
		var rng = MakeVisualRng(seed, salt);
		var angle = rng.RandfRange(0f, Mathf.Tau);
		var radius = preferEdge
			? Mathf.Lerp(0.34f, 0.60f, composition.PatchSpread)
			: Mathf.Lerp(0.12f, 0.34f, 1.0f - composition.PatchSpread * 0.45f);
		return new Vector3(Mathf.Cos(angle) * radius, 0, Mathf.Sin(angle) * radius);
	}

	private MeshInstance3D CreateMeshNode(Mesh mesh, Vector3 position, TileType tileType, bool preview, bool slab)
	{
		return new MeshInstance3D
		{
			Mesh = mesh,
			Position = position,
			Rotation = slab ? new Vector3(0, Mathf.Pi / 6.0f, 0) : Vector3.Zero,
			MaterialOverride = GetTileMaterial(tileType, preview, slab),
			CastShadow = GeometryInstance3D.ShadowCastingSetting.On
		};
	}

	private MeshInstance3D CreateColoredMesh(Mesh mesh, Vector3 position, Color color, bool preview)
	{
		var meshInstance = new MeshInstance3D
		{
			Mesh = mesh,
			Position = position,
			MaterialOverride = CreateColorMaterial(color, preview),
			CastShadow = GeometryInstance3D.ShadowCastingSetting.On
		};
		if (meshInstance.MaterialOverride is StandardMaterial3D mat)
			ApplyMaterialJitter(mat, (int)(position.X * 1000f + position.Z * 1000f), 8000, 0.06f, 1.0f);
		return meshInstance;
	}

	private StandardMaterial3D GetTileMaterial(TileType tileType, bool preview, bool slab)
	{
		var target = preview ? _previewTileMaterials : _solidTileMaterials;
		if (target.TryGetValue(tileType, out var cached))
			return cached;

		var color = tileType switch
		{
			TileType.Plains => new Color(0.64f, 0.88f, 0.46f),
			TileType.Woods => new Color(0.43f, 0.76f, 0.43f),
			TileType.River => new Color(0.48f, 0.82f, 0.98f),
			TileType.Meadow => new Color(0.79f, 0.94f, 0.56f),
			TileType.Village => new Color(0.98f, 0.78f, 0.63f),
			TileType.Lake => new Color(0.43f, 0.78f, 0.98f),
			_ => Colors.Magenta
		};

		if (slab)
			color = color.Darkened(0.05f);

		cached = CreateColorMaterial(color, preview);
		target[tileType] = cached;
		return cached;
	}

	private static StandardMaterial3D CreateColorMaterial(Color color, bool preview)
	{
		var mat = new StandardMaterial3D
		{
			AlbedoColor = preview ? new Color(color.R, color.G, color.B, 0.45f) : color,
			Roughness = 0.9f
		};
		if (preview)
			mat.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
		return mat;
	}

	private void ConfigureWaterMaterial(StandardMaterial3D mat, int seed, bool isLake, bool preview, bool highlightLayer)
	{
		mat.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
		mat.Metallic = Mathf.Clamp((isLake ? 0.11f : 0.08f) + (DeterministicJitter(seed, 100) * 0.03f), 0.05f, 0.15f);
		mat.Roughness = Mathf.Clamp((highlightLayer ? (isLake ? 0.03f : 0.05f) : (isLake ? 0.06f : 0.08f)) + (Mathf.Abs(DeterministicJitter(seed, 101)) * 0.03f), 0.02f, 0.12f);
		mat.RimEnabled = true;
		mat.Rim = highlightLayer ? 0.38f : 0.30f;
		mat.RimTint = highlightLayer ? 0.82f : 0.72f;

		var c = mat.AlbedoColor;
		var alpha = preview ? (highlightLayer ? 0.18f : 0.14f) : (highlightLayer ? 0.28f : 0.42f);
		mat.AlbedoColor = new Color(c.R, c.G, c.B, alpha);
	}

	private void BuildWetShoreSegments(Node3D root, int seed, bool preview, bool isLake, float yaw, float radius)
	{
		var rng = MakeVisualRng(seed, isLake ? 6400 : 3400);
		var count = rng.RandiRange(6, 10);
		for (var i = 0; i < count; i++)
		{
			var angle = (Mathf.Tau * i / count) + rng.RandfRange(-0.08f, 0.08f);
			if (!isLake)
				angle = yaw + (angle * 0.55f) + Mathf.Pi * 0.5f; // cluster around river band

			var seg = CreateColoredMesh(
				new BoxMesh { Size = new Vector3(rng.RandfRange(0.10f, 0.20f), 0.008f, rng.RandfRange(0.04f, 0.07f)) },
				new Vector3(Mathf.Cos(angle) * radius, isLake ? 0.148f : 0.144f, Mathf.Sin(angle) * radius),
				new Color(0.23f, 0.31f, 0.22f),
				preview);
			seg.Rotation = new Vector3(0, -angle + (isLake ? 0f : 0.25f), 0);
			if (seg.MaterialOverride is StandardMaterial3D shoreMat)
				ApplyMaterialJitter(shoreMat, seed, 6500 + i, 0.04f, 0.92f);
			root.AddChild(seg);
		}
	}

	private void BuildWaterRipples(Node3D root, int slotIndex, bool isLake, float yaw, float baseRadius, float y)
	{
		var rng = MakeVisualRng(slotIndex, isLake ? 6600 : 3600);
		var count = rng.RandiRange(6, 10);
		var ripples = new List<RippleAnim>(count);

		for (var i = 0; i < count; i++)
		{
			var ring = new Node3D { Name = $"Ripple{i}", Position = new Vector3(0, y + rng.RandfRange(-0.002f, 0.003f), 0), Rotation = new Vector3(0, yaw + rng.RandfRange(-0.1f, 0.1f), 0) };
			root.AddChild(ring);

			var mat = new StandardMaterial3D
			{
				Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
				ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
				AlbedoColor = new Color(0.78f, 0.94f, 1f, 0.16f)
			};

			var segments = 8;
			var ringRadius = baseRadius * rng.RandfRange(0.48f, 1.0f);
			for (var s = 0; s < segments; s++)
			{
				var a = Mathf.Tau * s / segments;
				var piece = new MeshInstance3D
				{
					Mesh = new BoxMesh { Size = new Vector3(0.10f, 0.003f, 0.018f) },
					Position = new Vector3(Mathf.Cos(a) * ringRadius, 0, Mathf.Sin(a) * ringRadius),
					Rotation = new Vector3(0, -a, 0),
					MaterialOverride = mat,
					CastShadow = GeometryInstance3D.ShadowCastingSetting.Off
				};
				ring.AddChild(piece);
			}

			var phase = (Mathf.Abs(DeterministicJitter(slotIndex, 6700 + i)) * Mathf.Tau);
			var speed = 0.6f + Mathf.Abs(DeterministicJitter(slotIndex, 6800 + i)) * 0.6f;
			var scaleDelta = 0.02f + Mathf.Abs(DeterministicJitter(slotIndex, 6900 + i)) * 0.03f;
			var alphaBase = 0.10f + Mathf.Abs(DeterministicJitter(slotIndex, 7000 + i)) * 0.06f;
			var alphaDelta = 0.05f + Mathf.Abs(DeterministicJitter(slotIndex, 7100 + i)) * 0.07f;

			ripples.Add(new RippleAnim(ring, mat, phase, speed, scaleDelta, alphaBase, alphaDelta));
		}

		_slotRipples[slotIndex] = ripples;
	}

	private void UpdateWaterRipples()
	{
		if (_slotRipples.Count == 0)
			return;

		foreach (var pair in _slotRipples)
		{
			foreach (var ripple in pair.Value)
			{
				if (!GodotObject.IsInstanceValid(ripple.Root))
					continue;

				var wave = Mathf.Sin((_time * ripple.Speed) + ripple.Phase);
				var scale = 1.0f + (wave * ripple.ScaleDelta);
				ripple.Root.Scale = new Vector3(scale, 1f, scale);

				var alpha = Mathf.Clamp(ripple.BaseAlpha + (wave * ripple.AlphaDelta), 0.03f, 0.35f);
				var c = ripple.Material.AlbedoColor;
				ripple.Material.AlbedoColor = new Color(c.R, c.G, c.B, alpha);
			}
		}
	}

	private void RemoveRipplesForSlot(int slotIndex)
	{
		_slotRipples.Remove(slotIndex);
	}

	private void AddMicroLumps(Node3D root, int seed, bool preview, int count, Color color)
	{
		var rng = MakeVisualRng(seed, 9000);
		for (var i = 0; i < count; i++)
		{
			var pos = RandomTopPoint(rng, 0.62f);
			var lump = CreateColoredMesh(
				new SphereMesh
				{
					Radius = rng.RandfRange(0.03f, 0.06f),
					Height = rng.RandfRange(0.02f, 0.05f),
					RadialSegments = 8,
					Rings = 4
				},
				pos + new Vector3(0, 0.205f + rng.RandfRange(-0.005f, 0.01f), 0),
				color,
				preview);
			if (lump.MaterialOverride is StandardMaterial3D mat)
				ApplyMaterialJitter(mat, seed, 9100 + i, 0.05f, 0.95f);
			root.AddChild(lump);
		}
	}

	private TileComposition GetTileComposition(TileType tileType, int seed, NeighborTypes neighbors)
	{
		var northWater = IsWater(neighbors.North);
		var eastWater = IsWater(neighbors.East);
		var southWater = IsWater(neighbors.South);
		var westWater = IsWater(neighbors.West);
		var waterNeighbors = (northWater ? 1 : 0) + (eastWater ? 1 : 0) + (southWater ? 1 : 0) + (westWater ? 1 : 0);

		var waterBase = tileType switch
		{
			TileType.River => 0.50f,
			TileType.Lake => 0.66f,
			_ => 0.08f
		};
		var waterVariance = tileType switch
		{
			TileType.River => 0.28f,
			TileType.Lake => 0.24f,
			_ => 0.05f
		};

		var waterDominance = Mathf.Clamp(
			waterBase +
			(DeterministicJitter(seed, 12001 + (int)tileType) * waterVariance) +
			(waterNeighbors * (tileType is TileType.River or TileType.Lake ? 0.08f : 0.0f)),
			tileType is TileType.River ? 0.20f : (tileType == TileType.Lake ? 0.34f : 0.0f),
			tileType is TileType.River ? 0.92f : (tileType == TileType.Lake ? 0.96f : 0.28f));

		var patchSpread = Mathf.Clamp(0.42f + (DeterministicJitter(seed, 12041 + (int)tileType) * 0.30f), 0.12f, 0.92f);
		var patchDensity = Mathf.Clamp(0.52f + (DeterministicJitter(seed, 12071 + (int)tileType) * 0.26f), 0.20f, 0.95f);
		var detailScale = Mathf.Clamp(1.0f + (DeterministicJitter(seed, 12101 + (int)tileType) * 0.18f), 0.78f, 1.25f);
		var detailBias = Math.Clamp((int)MathF.Round(DeterministicJitter(seed, 12131 + (int)tileType) * 2.0f), -1, 2);
		var dryness = Mathf.Clamp(0.45f + (DeterministicJitter(seed, 12161 + (int)tileType) * 0.22f), 0.08f, 0.92f);
		var heightScale = Mathf.Clamp(1.0f + (DeterministicJitter(seed, 12191 + (int)tileType) * 0.16f), 0.78f, 1.22f);

		return new TileComposition(
			WaterDominance: waterDominance,
			PatchSpread: patchSpread,
			PatchDensity: patchDensity,
			PatchDensityScale: Mathf.Lerp(0.88f, 1.18f, patchDensity),
			DetailScale: detailScale,
			DetailBias: detailBias,
			Dryness: dryness,
			HeightScale: heightScale);
	}

	private static float DeterministicJitter(int seed, int salt)
	{
		var x = (seed * 73856093) ^ (salt * 19349663);
		x ^= x >> 13;
		x *= 1274126177;
		x ^= x >> 16;
		return ((x & 1023) / 511.5f) - 1.0f;
	}

	private static void ApplyMaterialJitter(StandardMaterial3D mat, int seed, int salt, float roughnessRange, float albedoScale)
	{
		var jitter = DeterministicJitter(seed, salt);
		mat.Roughness = Mathf.Clamp(mat.Roughness + (jitter * roughnessRange), 0.02f, 0.98f);
		var shade = Mathf.Clamp(albedoScale + (DeterministicJitter(seed, salt + 17) * 0.05f), 0.82f, 1.08f);
		mat.AlbedoColor = new Color(mat.AlbedoColor.R * shade, mat.AlbedoColor.G * shade, mat.AlbedoColor.B * shade, mat.AlbedoColor.A);
	}

	private void FocusCamera()
	{
		_cameraYaw = 0.75f;
		_cameraPitch = GetDefaultCameraPitchForViewport();
		_cameraDistance = GetDefaultCameraDistanceForViewport();
		_cameraUserAdjusted = false;
		_cameraFocusOffset = Vector3.Zero;
		_cameraFocusSmoothedOffset = Vector3.Zero;
		_placementFocusSlot = null;
		_placementFocusTimer = 0f;
		_cameraPositionInitialized = false;
		UpdateCamera();
		SetStatus("Camera centered.");
	}

	private float GetDefaultCameraDistanceForViewport()
	{
		var size = GetViewport().GetVisibleRect().Size;
		if (size.Y <= 0f)
			return 14.0f;

		var aspect = size.X / size.Y;
		var baseDistance = 14.2f;

		// Wider screens can move closer; tall/narrow screens need more distance.
		baseDistance += Mathf.Lerp(1.4f, -1.0f, Mathf.Clamp((aspect - 1.35f) / 0.85f, 0f, 1f));

		// If utility rail is open in minimal mode, pull back slightly to preserve center readability.
		if (_minimalPlayMode && _utilityPanelVisible)
			baseDistance += 0.8f;

		return Mathf.Clamp(baseDistance, 10.5f, 17.8f);
	}

	private float GetDefaultCameraPitchForViewport()
	{
		var size = GetViewport().GetVisibleRect().Size;
		if (size.Y <= 0f)
			return -0.72f;

		var aspect = size.X / size.Y;
		var t = Mathf.Clamp((aspect - 1.35f) / 0.85f, 0f, 1f);
		return Mathf.Lerp(-0.66f, -0.76f, t);
	}

	private void UpdateCamera()
	{
		_cameraPivot.Position = _cameraFocusSmoothedOffset;
		var dir = new Vector3(
			Mathf.Cos(_cameraPitch) * Mathf.Sin(_cameraYaw),
			Mathf.Sin(-_cameraPitch),
			Mathf.Cos(_cameraPitch) * Mathf.Cos(_cameraYaw));

		var targetPos = _cameraPivot.GlobalPosition + (dir * _cameraDistance);
		if (!_cameraPositionInitialized)
		{
			_cameraSmoothedPosition = targetPos;
			_cameraPositionInitialized = true;
		}

		var smooth = _orbitDragging ? 1.0f : 0.15f;
		_cameraSmoothedPosition = _cameraSmoothedPosition.Lerp(targetPos, smooth);
		_camera.GlobalPosition = _cameraSmoothedPosition;
		_camera.LookAt(_cameraPivot.GlobalPosition, Vector3.Up);
	}

	private void UpdateCameraFocusOffset(float delta)
	{
		var desired = Vector3.Zero;

		if (_placementFocusTimer > 0f && _placementFocusSlot is int placedSlot && _slots.TryGetValue(placedSlot, out var placedVisual))
		{
			_placementFocusTimer = Mathf.Max(0f, _placementFocusTimer - delta);
			var t = _placementFocusTimer / 0.45f;
			var ease = Mathf.Sin(t * Mathf.Pi);
			desired += placedVisual.Root.Position * (0.10f * ease);
			if (_placementFocusTimer <= 0f)
				_placementFocusSlot = null;
		}

		if (!_orbitDragging && _hoveredSlotIndex is int hoveredSlot && _slots.TryGetValue(hoveredSlot, out var hoveredVisual))
		{
			var hoverStrength = _utilityPanelVisible ? 0.08f : 0.12f;
			desired += hoveredVisual.Root.Position * hoverStrength;
		}

		_cameraFocusOffset = desired;
		var smooth = _orbitDragging ? 0.18f : 0.14f;
		_cameraFocusSmoothedOffset = _cameraFocusSmoothedOffset.Lerp(_cameraFocusOffset, smooth);
	}

	private void UpdatePerformanceMetrics(float delta)
	{
		_perfSampleTimer -= delta;
		if (_perfSampleTimer > 0f)
			return;

		_perfSampleTimer = 0.6f;
		var (nodeCount, meshCount) = CountSceneVisualStats(this);
		_lastNodeCount = nodeCount;
		_lastMeshInstanceCount = meshCount;

		if (!_perfWarned && meshCount > 150)
		{
			_perfWarned = true;
			_eventSink.Publish(new UiNoteEvent($"Perf warning: mesh instances {meshCount} > 150"));
		}
	}

	private static (int NodeCount, int MeshInstanceCount) CountSceneVisualStats(Node root)
	{
		var stack = new Stack<Node>();
		stack.Push(root);
		var nodes = 0;
		var meshes = 0;

		while (stack.Count > 0)
		{
			var current = stack.Pop();
			nodes++;
			if (current is MeshInstance3D)
				meshes++;

			foreach (var child in current.GetChildren())
			{
				if (child is Node childNode)
					stack.Push(childNode);
			}
		}

		return (nodes, meshes);
	}

	private Vector3 GetSlotPosition(int slotIndex)
	{
		var (col, row) = ToXY(slotIndex);
		var x = (col - 1) * HexSpacingX + ((row % 2 == 0) ? 0.0f : HexSpacingX * 0.5f);
		var z = (row - 1) * HexSpacingZ;
		return new Vector3(x, 0, z);
	}

	private string ResolveSavePath()
	{
		var path = string.IsNullOrWhiteSpace(_savePathEdit.Text) ? DefaultSavePath : _savePathEdit.Text;
		return ProjectSettings.GlobalizePath(path);
	}

	private string ResolveHighScorePath() => ProjectSettings.GlobalizePath(DefaultHighScorePath);

	private void SetStatus(string message) => _statusLabel.Text = $"Status {CompactText(message, _minimalPlayMode ? 74 : 120)}";

	private static string CompactText(string text, int maxLen)
	{
		if (string.IsNullOrWhiteSpace(text))
			return "-";

		var compact = text.Replace('\r', ' ').Replace('\n', ' ').Trim();
		return compact.Length <= maxLen ? compact : (compact[..Math.Max(1, maxLen - 1)] + "…");
	}

	private static Color GetTileSwatchColor(TileType? tileType) => tileType switch
	{
		TileType.Plains => new Color(0.56f, 0.73f, 0.38f),
		TileType.Woods => new Color(0.30f, 0.46f, 0.24f),
		TileType.River => new Color(0.30f, 0.60f, 0.75f),
		TileType.Meadow => new Color(0.64f, 0.76f, 0.42f),
		TileType.Village => new Color(0.64f, 0.52f, 0.40f),
		TileType.Lake => new Color(0.24f, 0.50f, 0.64f),
		_ => new Color(0.88f, 0.92f, 0.94f)
	};

	private TileType? GetPlacedTileType(int slotIndex)
	{
		if (_session is null)
			return null;

		return _session.State.Board.TryGetValue(slotIndex, out var placed) ? placed.TileType : null;
	}

	private NeighborTypes GetNeighborTypes(int slotIndex)
	{
		var (x, y) = ToXY(slotIndex);

		TileType? north = y > 0 ? GetPlacedTileType(slotIndex - GameState.BoardWidth) : null;
		TileType? east = x < GameState.BoardWidth - 1 ? GetPlacedTileType(slotIndex + 1) : null;
		TileType? south = y < GameState.BoardHeight - 1 ? GetPlacedTileType(slotIndex + GameState.BoardWidth) : null;
		TileType? west = x > 0 ? GetPlacedTileType(slotIndex - 1) : null;

		return new NeighborTypes(north, east, south, west);
	}

	private TileVisualSignature BuildVisualSignature(int slotIndex, TileType tileType, int rotationQuarterTurns)
	{
		var neighbors = GetNeighborTypes(slotIndex);
		return new TileVisualSignature(tileType, rotationQuarterTurns, neighbors.North, neighbors.East, neighbors.South, neighbors.West);
	}

	private static IEnumerable<NeighborDir> CardinalDirs()
	{
		yield return NeighborDir.North;
		yield return NeighborDir.East;
		yield return NeighborDir.South;
		yield return NeighborDir.West;
	}

	private static Vector3 DirectionVector(NeighborDir dir) => dir switch
	{
		NeighborDir.North => new Vector3(0, 0, -1),
		NeighborDir.East => new Vector3(1, 0, 0),
		NeighborDir.South => new Vector3(0, 0, 1),
		_ => new Vector3(-1, 0, 0)
	};

	private static float DirectionYaw(NeighborDir dir) => dir switch
	{
		NeighborDir.North => 0f,
		NeighborDir.East => -Mathf.Pi * 0.5f,
		NeighborDir.South => Mathf.Pi,
		_ => Mathf.Pi * 0.5f
	};

	private static (int X, int Y) ToXY(int slotIndex) => (slotIndex % GameState.BoardWidth, slotIndex / GameState.BoardWidth);

	private sealed class SlotVisual
	{
		public SlotVisual(int slotIndex, Node3D root, MeshInstance3D baseHex, StandardMaterial3D baseMaterial, Node3D overlay, Node3D tileRoot, float tileRootBaseY, float idlePhase)
		{
			SlotIndex = slotIndex;
			Root = root;
			BaseHex = baseHex;
			BaseMaterial = baseMaterial;
			Overlay = overlay;
			TileRoot = tileRoot;
			TileRootBaseY = tileRootBaseY;
			IdlePhase = idlePhase;
		}

		public int SlotIndex { get; }
		public Node3D Root { get; }
		public MeshInstance3D BaseHex { get; }
		public StandardMaterial3D BaseMaterial { get; }
		public Node3D Overlay { get; }
		public Node3D TileRoot { get; }
		public float TileRootBaseY { get; }
		public float IdlePhase { get; }
	}

	private enum NeighborDir
	{
		North = 0,
		East = 1,
		South = 2,
		West = 3
	}

	private readonly record struct NeighborTypes(TileType? North, TileType? East, TileType? South, TileType? West)
	{
		public TileType? this[NeighborDir dir] => dir switch
		{
			NeighborDir.North => North,
			NeighborDir.East => East,
			NeighborDir.South => South,
			_ => West
		};
	}

	private readonly record struct TileVisualSignature(
		TileType TileType,
		int Rotation,
		TileType? North,
		TileType? East,
		TileType? South,
		TileType? West);

	private readonly record struct TileComposition(
		float WaterDominance,
		float PatchSpread,
		float PatchDensity,
		float PatchDensityScale,
		float DetailScale,
		int DetailBias,
		float Dryness,
		float HeightScale);

	private sealed class RippleAnim
	{
		public RippleAnim(Node3D root, StandardMaterial3D material, float phase, float speed, float scaleDelta, float baseAlpha, float alphaDelta)
		{
			Root = root;
			Material = material;
			Phase = phase;
			Speed = speed;
			ScaleDelta = scaleDelta;
			BaseAlpha = baseAlpha;
			AlphaDelta = alphaDelta;
		}

		public Node3D Root { get; }
		public StandardMaterial3D Material { get; }
		public float Phase { get; }
		public float Speed { get; }
		public float ScaleDelta { get; }
		public float BaseAlpha { get; }
		public float AlphaDelta { get; }
	}

	private sealed class UiEventSink : IEventSink
	{
		private readonly List<string> _lines = new();
		public IReadOnlyList<string> RecentLines => _lines;

		public void Publish(IDomainEvent domainEvent)
		{
			_lines.Add(domainEvent.ToString() ?? domainEvent.GetType().Name);
			if (_lines.Count > 8)
				_lines.RemoveAt(0);
		}

		public void Clear() => _lines.Clear();
	}

	private sealed class UiNoteEvent : IDomainEvent
	{
		private readonly string _message;

		public UiNoteEvent(string message) => _message = message;

		public override string ToString() => _message;
	}
}
