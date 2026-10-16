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
	private const string DefaultHighScorePath = "user://highscore.dat";
	private const int DefaultDeckSize = 30;
	private const float HexRadius = 1.0f;
	private const float MinCameraPitch = -1.2566371f; // -72 degrees
	private const float MaxCameraPitch = -0.4886922f; // -28 degrees

	private enum WorldTheme
	{
		Temperate = 0,
		Tropical = 1,
		Frontier = 2
	}

	private readonly SaveRepository _saveRepository = new();
	private readonly HighScoreRepository _highScoreRepository = new();
	private readonly UiEventSink _eventSink = new();
	private readonly FastNoiseLite _terrainNoise = new();
	private readonly Dictionary<int, SlotVisual> _slots = new();
	private readonly Dictionary<TileType, StandardMaterial3D> _solidTileMaterials = new();
	private readonly Dictionary<TileType, StandardMaterial3D> _previewTileMaterials = new();
	private readonly Dictionary<int, TileVisualSignature> _renderedTiles = new();
	private readonly Dictionary<int, List<RippleAnim>> _slotRipples = new();
	private readonly Dictionary<TileType, string> _tileScenePaths = new()
	{
		[TileType.Plains] = "res://scenes/tiles/PlainsTile.tscn",
		[TileType.Woods] = "res://scenes/tiles/ForestTile.tscn",
		[TileType.Village] = "res://scenes/tiles/CityTile.tscn",
		[TileType.Lake] = "res://scenes/tiles/MountainTile.tscn",
		[TileType.Meadow] = "res://scenes/tiles/FarmlandTile.tscn",
		[TileType.River] = "res://scenes/tiles/BeachTile.tscn",
		[TileType.Desert] = "res://scenes/tiles/DesertTile.tscn",
		[TileType.Snow] = "res://scenes/tiles/SnowTile.tscn",
		[TileType.Volcano] = "res://scenes/tiles/VolcanoTile.tscn",
		[TileType.Pond] = "res://scenes/tiles/LakeTile.tscn",
		[TileType.Ruins] = "res://scenes/tiles/RuinsTile.tscn",
		[TileType.Swamp] = "res://scenes/tiles/ForestTile.tscn",
		[TileType.Savanna] = "res://scenes/tiles/PlainsTile.tscn",
		[TileType.Tundra] = "res://scenes/tiles/SnowTile.tscn",
		[TileType.Coral] = "res://scenes/tiles/LakeTile.tscn",
		[TileType.Mushroom] = "res://scenes/tiles/ForestTile.tscn"
	};
	private readonly Dictionary<TileType, PackedScene> _tileScenes = new();

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

	private Label _coinsLabel = null!;
	private Label _scoreLabel = null!;
	private Label _highScoreLabel = null!;
	private Label _currentTileLabel = null!;
	private Label _rotationLabel = null!;
	private Label _unlockedTilesLabel = null!;
	private Label _lastMessageLabel = null!;
	private Label _statusLabel = null!;
	private Label _comboLabel = null!;
	private Label _nextLabel = null!;
	private PanelContainer _tilePreviewPanel = null!;
	private TextureRect _tilePreviewRect = null!;
	private TextureRect _nextPreviewRect = null!;
	private Label _tutorialHintLabel = null!;
	private PanelContainer _tutorialHintPanel = null!;
	private RichTextLabel _eventLog = null!;
	private Label _eventTitleLabel = null!;
	private PanelContainer _actionsRailPanel = null!;
	private PanelContainer _eventPanel = null!;
	private ColorRect _menuOverlay = null!;
	private PanelContainer _menuPanel = null!;
	private Button _utilityButton = null!;
	private Button _resumeButton = null!;
	private Button _overlayNewGameButton = null!;
	private Button _quitButton = null!;
	private Button _submitScoreButton = null!;
	private Button _saveButton = null!;
	private Button _loadButton = null!;
	private Button _refreshHighScoreButton = null!;
	private LineEdit _seedEdit = null!;
	private LineEdit _savePathEdit = null!;
	private VBoxContainer _unlockButtons = null!;
	private PanelContainer _unlockPanel = null!;
	private Button _unlockToggleButton = null!;

	private Node3D _previewRoot = null!;
	private Node3D _previewVisual = null!;
	private bool _hasPreviewVisual;
	private TileType? _previewTileType;
	private Node3D _decorRoot = null!;
	private Node3D _hoverRingRoot = null!;
	private SubViewport _inHandPreviewViewport = null!;
	private Node3D _inHandPreviewRoot = null!;
	private Camera3D _inHandPreviewCamera = null!;
	private DirectionalLight3D _inHandPreviewLight = null!;
	private Node3D _inHandPreviewTile = null!;
	private SubViewport _nextPreviewViewport = null!;
	private Node3D _nextPreviewRoot = null!;
	private Camera3D _nextPreviewCamera = null!;
	private DirectionalLight3D _nextPreviewLight = null!;
	private Node3D _nextPreviewTile = null!;
	private StandardMaterial3D _hoverRingMaterial = null!;
	private Tween _hoverRingTween = null!;
	private Color _hoverRingBaseColor = new(1f, 1f, 1f, 0.0f);
	private float _hoverRingAlpha;
	private StandardMaterial3D _validPreviewOverlay = null!;
	private StandardMaterial3D _invalidPreviewOverlay = null!;
	private MeshInstance3D _waterSurface = null!;
	private float _time;

	private int _rotationQuarterTurns;
	private int? _hoveredSlotIndex;
	private bool _showEmptyOverlays;
	private bool _orbitDragging;
	private bool _isPanning;
	private bool _cameraUserAdjusted;
	private Vector2 _lastMousePos;
	private Vector2 _lastPanMousePos;
	private Vector3 _cameraFocusOffset;
	private Vector3 _cameraFocusSmoothedOffset;
	private Vector3 _cameraPanOffset;
	private float _cameraYaw = 0.75f;
	private float _cameraPitch = -0.87266463f;
	private float _cameraDistance = 8.0f;
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
	private Vector3 _boardCentroid;
	private Vector3 _boardCentroidSmoothed;
	private float _autoDrawTimer;
	private bool _autoDrawPending;
	private readonly List<FloatingPopup> _floatingPopups = new();
	private float _hexOuterRadius = HexRadius;
	private float _hexSpacingX = HexRadius * 1.7320508f;
	private float _hexSpacingZ = HexRadius * 1.5f;
	private ArrayMesh _sharedHexPrismMesh = null!;
	private TileType? _inHandPreviewTileType;
	private ulong _inHandPreviewSeed;
	private SectorPattern _currentHandPattern = SectorPattern.Pure(TileType.Plains);
	private ulong _currentHandSeed;
	private bool _menuVisible;
	private int _comboCount;
	private int _bestCombo;
	private int _deckRemaining;
	private int _highScoreValue;
	private TileType? _queuedTileType;
	private ulong _queuedTileSeed;
	private SectorPattern _queuedTilePattern = SectorPattern.Pure(TileType.Plains);
	private TileType? _nextPreviewTileType;
	private WorldTheme _activeTheme = WorldTheme.Temperate;

	public float HoverRingAlpha
	{
		get => _hoverRingAlpha;
		set
		{
			_hoverRingAlpha = value;
			if (IsAlive(_hoverRingMaterial))
				_hoverRingMaterial.AlbedoColor = new Color(_hoverRingBaseColor.R, _hoverRingBaseColor.G, _hoverRingBaseColor.B, value);
			if (IsAlive(_hoverRingRoot))
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
		_keyBaseEnergy = _keyLight.LightEnergy;
		_keyBaseRotation = _keyLight.Rotation;
		_keyBaseColor = _keyLight.LightColor;
		_fillBaseColor = _fillLight.LightColor;
		_bounceBaseColor = _bounceLight.LightColor;

		_coinsLabel = GetNode<Label>("CanvasLayer/HUDRoot/TopLeftPanel/CurrencyRow/CoinsChip/CoinsLabel");
		_scoreLabel = GetNode<Label>("CanvasLayer/HUDRoot/TopLeftPanel/CurrencyRow/ScoreChip/ScoreLabel");
		_highScoreLabel = GetNode<Label>("CanvasLayer/HUDRoot/TopLeftPanel/CurrencyRow/HighScoreChip/HighScoreLabel");
		_utilityButton = GetNode<Button>("CanvasLayer/HUDRoot/TopLeftPanel/CurrencyRow/UtilityButton");
		_tilePreviewPanel = GetNode<PanelContainer>("CanvasLayer/HUDRoot/TileDeckPanel/DeckVBox/TileCardRow/TilePreviewPanel");
		_tilePreviewRect = GetNode<TextureRect>("CanvasLayer/HUDRoot/TileDeckPanel/DeckVBox/TileCardRow/TilePreviewPanel/TilePreviewTexture");
		_currentTileLabel = GetNode<Label>("CanvasLayer/HUDRoot/TileDeckPanel/DeckVBox/TileCardRow/TileCardText/CurrentTileLabel");
		_rotationLabel = GetNode<Label>("CanvasLayer/HUDRoot/TileDeckPanel/DeckVBox/TileCardRow/TileCardText/RotationLabel");
		_nextLabel = GetNode<Label>("CanvasLayer/HUDRoot/TileDeckPanel/DeckVBox/NextRow/NextText/NextLabel");
		_nextPreviewRect = GetNode<TextureRect>("CanvasLayer/HUDRoot/TileDeckPanel/DeckVBox/NextRow/NextPreviewPanel/NextPreviewTexture");
		_unlockedTilesLabel = GetNode<Label>("CanvasLayer/HUDRoot/TileDeckPanel/DeckVBox/UnlockedTilesLabel");
		_tutorialHintPanel = GetNode<PanelContainer>("CanvasLayer/HUDRoot/TileDeckPanel/DeckVBox/TutorialHintPanel");
		_tutorialHintLabel = GetNode<Label>("CanvasLayer/HUDRoot/TileDeckPanel/DeckVBox/TutorialHintPanel/TutorialHintLabel");
		_lastMessageLabel = GetNode<Label>("CanvasLayer/HUDRoot/TileDeckPanel/DeckVBox/LastMessageLabel");
		_statusLabel = GetNodeOrNull<Label>("CanvasLayer/HUDRoot/TileDeckPanel/DeckVBox/StatusLabel");
		_comboLabel = GetNode<Label>("CanvasLayer/HUDRoot/ComboLabel");
		_eventLog = GetNode<RichTextLabel>("CanvasLayer/HUDRoot/EventPanel/EventVBox/EventLog");
		_eventTitleLabel = GetNode<Label>("CanvasLayer/HUDRoot/EventPanel/EventVBox/EventTitle");
		_actionsRailPanel = GetNode<PanelContainer>("CanvasLayer/HUDRoot/ActionsRail");
		_eventPanel = GetNode<PanelContainer>("CanvasLayer/HUDRoot/EventPanel");
		_menuOverlay = GetNode<ColorRect>("CanvasLayer/HUDRoot/MenuOverlay");
		_menuPanel = GetNode<PanelContainer>("CanvasLayer/HUDRoot/MenuOverlay/MenuPanel");
		_resumeButton = GetNode<Button>("CanvasLayer/HUDRoot/MenuOverlay/MenuPanel/MenuVBox/ResumeButton");
		_overlayNewGameButton = GetNode<Button>("CanvasLayer/HUDRoot/MenuOverlay/MenuPanel/MenuVBox/OverlayNewGameButton");
		_quitButton = GetNode<Button>("CanvasLayer/HUDRoot/MenuOverlay/MenuPanel/MenuVBox/QuitButton");
		_submitScoreButton = GetNode<Button>("CanvasLayer/HUDRoot/ActionsRail/RailVBox/SubmitScoreButton");
		_saveButton = GetNode<Button>("CanvasLayer/HUDRoot/ActionsRail/RailVBox/SaveButton");
		_loadButton = GetNode<Button>("CanvasLayer/HUDRoot/ActionsRail/RailVBox/LoadButton");
		_refreshHighScoreButton = GetNode<Button>("CanvasLayer/HUDRoot/ActionsRail/RailVBox/RefreshHighScoreButton");
		_seedEdit = GetNode<LineEdit>("CanvasLayer/HUDRoot/ActionsRail/RailVBox/SeedRow/SeedEdit");
		_savePathEdit = GetNode<LineEdit>("CanvasLayer/HUDRoot/ActionsRail/RailVBox/SavePathEdit");
		_unlockButtons = GetNode<VBoxContainer>("CanvasLayer/HUDRoot/ActionsRail/RailVBox/UnlockPanel/UnlockScroll/UnlockButtons");
		_unlockPanel = GetNode<PanelContainer>("CanvasLayer/HUDRoot/ActionsRail/RailVBox/UnlockPanel");
		_unlockToggleButton = GetNode<Button>("CanvasLayer/HUDRoot/ActionsRail/RailVBox/UnlockToggleButton");

		_seedEdit.Text = DefaultSeed.ToString();
		_savePathEdit.Text = DefaultSavePath;
		_terrainNoise.NoiseType = FastNoiseLite.NoiseTypeEnum.Simplex;
		_terrainNoise.Frequency = 0.65f;
		_validPreviewOverlay = CreatePreviewOverlayMaterial(new Color(0.20f, 0.92f, 0.80f, 0.38f));
		_invalidPreviewOverlay = CreatePreviewOverlayMaterial(new Color(0.96f, 0.34f, 0.36f, 0.38f));

		_decorRoot = new Node3D { Name = "DecorRoot" };
		AddChild(_decorRoot);
		_previewRoot = new Node3D { Name = "PreviewRoot" };
		_boardRoot.AddChild(_previewRoot);
		_inHandPreviewViewport = GetNode<SubViewport>("CanvasLayer/HUDRoot/InHandPreviewViewport");
		_inHandPreviewRoot = GetNode<Node3D>("CanvasLayer/HUDRoot/InHandPreviewViewport/PreviewSceneRoot");
		_inHandPreviewCamera = GetNode<Camera3D>("CanvasLayer/HUDRoot/InHandPreviewViewport/PreviewCamera3D");
		_inHandPreviewLight = GetNode<DirectionalLight3D>("CanvasLayer/HUDRoot/InHandPreviewViewport/PreviewLight");
		_nextPreviewViewport = GetNode<SubViewport>("CanvasLayer/HUDRoot/NextPreviewViewport");
		_nextPreviewRoot = GetNode<Node3D>("CanvasLayer/HUDRoot/NextPreviewViewport/PreviewSceneRoot");
		_nextPreviewCamera = GetNode<Camera3D>("CanvasLayer/HUDRoot/NextPreviewViewport/PreviewCamera3D");
		_nextPreviewLight = GetNode<DirectionalLight3D>("CanvasLayer/HUDRoot/NextPreviewViewport/PreviewLight");

		InitializeHexMetrics();
		ConfigureWorldEnvironmentAndOcean();
		BuildBoardVisuals();
		BuildHoverRing();
		BuildUnlockButtons();
		WireUi();
		ApplyHudStyle();
		ConfigureMinimalHud();
		ApplyResponsiveHudLayout(force: true);
		ApplyUtilityPanelState(false);
		ConfigureInHandPreviewViewport();
		StartNewSession(DefaultSeed);
		ForceInitialCameraState();
		RefreshHighScoreLabel();
		Render();
	}

	public override void _Process(double delta)
	{
		_time += (float)delta;
		if (_deckRemaining == 0 && _menuVisible)
			_cameraYaw += (float)delta * 0.18f;
		EnforceCameraPitchClamp();
		UpdateHoverSlot();
		UpdateCameraFocusOffset((float)delta);
		UpdateKeyboardPan((float)delta);
		UpdateCamera();
		UpdateDaylightDrift();
		UpdateIdleMotion();
		UpdateWaterRipples();
		UpdatePlacementOutlines();
		UpdateAutoDraw((float)delta);
		UpdateFloatingPopups((float)delta);
		UpdateInHandPreview((float)delta);
		UpdateStatusFeedback((float)delta);
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
		if (key.Keycode == Key.Escape)
		{
			ToggleMenu();
			GetViewport().SetInputAsHandled();
			return;
		}

		if (key.Keycode == Key.Q)
		{
			_rotationQuarterTurns = ((_rotationQuarterTurns - 1) % 6 + 6) % 6;
			RefreshGhostTile();
			Render();
			GetViewport().SetInputAsHandled();
			return;
		}

		if (key.Keycode == Key.E)
		{
			_rotationQuarterTurns = (_rotationQuarterTurns + 1) % 6;
			RefreshGhostTile();
			Render();
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

		if (key.Keycode == Key.F3)
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
			if (mouseButton.ButtonIndex == MouseButton.Middle)
				_isPanning = false;
			return;
		}

		_lastMousePos = mouseButton.Position;

		if (mouseButton.ButtonIndex == MouseButton.WheelUp)
		{
			_cameraDistance = Mathf.Clamp(_cameraDistance - 0.8f, 5.8f, 16.0f);
			_cameraUserAdjusted = true;
			UpdateCamera();
			GetViewport().SetInputAsHandled();
			return;
		}

		if (mouseButton.ButtonIndex == MouseButton.WheelDown)
		{
			_cameraDistance = Mathf.Clamp(_cameraDistance + 0.8f, 5.8f, 16.0f);
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

		if (mouseButton.ButtonIndex == MouseButton.Middle)
		{
			_isPanning = true;
			_lastPanMousePos = mouseButton.Position;
			GetViewport().SetInputAsHandled();
			return;
		}

		if (mouseButton.ButtonIndex == MouseButton.Left)
			TryPlaceFromScreenPoint(mouseButton.Position);
	}

	private void HandleMouseMotion(InputEventMouseMotion motion)
	{
		_lastMousePos = motion.Position;
		if (_isPanning)
		{
			var delta = motion.Position - _lastPanMousePos;
			_lastPanMousePos = motion.Position;
			PanCamera(delta);
			GetViewport().SetInputAsHandled();
			return;
		}

		if (!_orbitDragging)
			return;

		_cameraYaw -= motion.Relative.X * 0.008f;
		_cameraPitch -= motion.Relative.Y * 0.006f;
		EnforceCameraPitchClamp();
		_cameraUserAdjusted = true;
		UpdateCamera();
		GetViewport().SetInputAsHandled();
	}

	private void PanCamera(Vector2 screenDelta)
	{
		var panSpeed = 0.012f * _cameraDistance;
		var right = _cameraPivot.Transform.Basis.X.Normalized();
		var forward = new Vector3(_cameraPivot.Transform.Basis.Z.X, 0f, _cameraPivot.Transform.Basis.Z.Z).Normalized();
		_cameraPanOffset -= right * screenDelta.X * panSpeed;
		_cameraPanOffset += forward * screenDelta.Y * panSpeed;
		_cameraUserAdjusted = true;
	}

	private void UpdateKeyboardPan(float delta)
	{
		var input = Vector2.Zero;
		if (Input.IsKeyPressed(Key.A) || Input.IsKeyPressed(Key.Left))
			input.X -= 1f;
		if (Input.IsKeyPressed(Key.D) || Input.IsKeyPressed(Key.Right))
			input.X += 1f;
		if (Input.IsKeyPressed(Key.W) || Input.IsKeyPressed(Key.Up))
			input.Y -= 1f;
		if (Input.IsKeyPressed(Key.S) || Input.IsKeyPressed(Key.Down))
			input.Y += 1f;

		if (input == Vector2.Zero)
			return;

		PanCamera(input.Normalized() * (delta * 90f));
	}

	private void WireUi()
	{
		_submitScoreButton.Pressed += () => Issue(new SubmitScoreCommand());
		_saveButton.Pressed += SaveGame;
		_loadButton.Pressed += LoadGame;
		_refreshHighScoreButton.Pressed += RefreshHighScoreLabel;
		GetNode<Button>("CanvasLayer/HUDRoot/ActionsRail/RailVBox/SeedRow/NewGameButton").Pressed += CreateNewGameFromSeed;
		_utilityButton.Pressed += ToggleMenu;
		_resumeButton.Pressed += ToggleMenu;
		_overlayNewGameButton.Pressed += () =>
		{
			ToggleMenu(forceVisible: false);
			StartNewSession(DefaultSeed);
			ForceInitialCameraState();
			Render();
		};
		_quitButton.Pressed += () => GetTree().Quit();

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
		if (_statusLabel is not null)
			_statusLabel.Visible = visible;
		_lastMessageLabel.Visible = visible;
		_unlockedTilesLabel.Visible = visible;
		_tutorialHintPanel.Visible = false;

		if (_minimalPlayMode)
		{
			_unlockPanel.Visible = false;
			_unlockToggleButton.Visible = false;
		}

		_utilityButton.Text = _utilityPanelVisible ? "Close" : "Menu";
		ApplyResponsiveHudLayout(force: true);
	}

	private void ToggleMenu()
	{
		ToggleMenu(!_menuVisible);
	}

	private void ToggleMenu(bool forceVisible)
	{
		_menuVisible = forceVisible;
		_resumeButton.Visible = true;
		_overlayNewGameButton.Text = "New Game";
		_quitButton.Text = "Quit";
		_menuPanel.GetNode<Label>("MenuVBox/MenuTitle").Text = "Paused";
		_menuOverlay.Visible = true;
		_menuOverlay.Modulate = new Color(1f, 1f, 1f, _menuVisible ? 0f : 1f);
		_menuPanel.Scale = _menuVisible ? new Vector2(0.96f, 0.96f) : Vector2.One;
		var tween = CreateTween();
		tween.SetEase(Tween.EaseType.Out);
		tween.SetTrans(Tween.TransitionType.Quad);
		tween.TweenProperty(_menuOverlay, "modulate", new Color(1f, 1f, 1f, _menuVisible ? 1f : 0f), 0.2);
		tween.Parallel().TweenProperty(_menuPanel, "scale", _menuVisible ? Vector2.One : new Vector2(0.96f, 0.96f), 0.2);
		if (!_menuVisible)
			tween.Finished += () => _menuOverlay.Visible = false;
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
			StylePanel(GetNode<PanelContainer>(path), new Color(0.08f, 0.12f, 0.15f, 0.82f), 16, 10, 10, 10, 10);

		StylePanel(GetNode<PanelContainer>("CanvasLayer/HUDRoot/TopLeftPanel"), new Color(0.06f, 0.10f, 0.14f, 0.54f), 18, 6, 6, 6, 6);
		StylePanel(GetNode<PanelContainer>("CanvasLayer/HUDRoot/ActionsRail"), new Color(0.05f, 0.08f, 0.10f, 0.84f), 22, 12, 12, 12, 12);
		StylePanel(GetNode<PanelContainer>("CanvasLayer/HUDRoot/TileDeckPanel"), new Color(0.05f, 0.08f, 0.10f, 0.88f), 22, 12, 12, 12, 12);
		StylePanel(GetNode<PanelContainer>("CanvasLayer/HUDRoot/EventPanel"), new Color(0.05f, 0.08f, 0.10f, 0.88f), 18, 8, 8, 8, 8);
		StylePanel(_menuPanel, new Color(0.06f, 0.09f, 0.12f, 0.94f), 20, 14, 14, 14, 14);
		StylePanel(_tilePreviewPanel, new Color(0.09f, 0.13f, 0.17f, 0.92f), 16, 4, 4, 4, 4);
		StylePanel(_unlockPanel, new Color(0.08f, 0.12f, 0.15f, 0.90f), 14, 6, 6, 6, 6);
		StylePanel(_tutorialHintPanel, new Color(0.05f, 0.08f, 0.10f, 0.84f), 14, 8, 8, 8, 8);

		_eventLog.ScrollFollowing = true;
		_eventLog.CustomMinimumSize = new Vector2(0, 72);
		_eventLog.AddThemeFontSizeOverride("normal_font_size", 10);
		_eventLog.AddThemeColorOverride("default_color", new Color(0.89f, 0.94f, 0.96f, 0.95f));
		_currentTileLabel.AddThemeFontSizeOverride("font_size", 15);
		_currentTileLabel.AddThemeColorOverride("font_color", new Color(0.92f, 0.96f, 0.95f));
		_nextLabel.AddThemeColorOverride("font_color", new Color(0.64f, 0.78f, 0.81f));
		_rotationLabel.AddThemeColorOverride("font_color", new Color(0.64f, 0.78f, 0.81f));
		_coinsLabel.AddThemeFontSizeOverride("font_size", 13);
		_scoreLabel.AddThemeFontSizeOverride("font_size", 13);
		_highScoreLabel.AddThemeFontSizeOverride("font_size", 13);
		_lastMessageLabel.AddThemeColorOverride("font_color", new Color(0.82f, 0.87f, 0.90f));
		_comboLabel.AddThemeFontSizeOverride("font_size", 24);
		_comboLabel.AddThemeColorOverride("font_color", new Color(1.0f, 0.86f, 0.30f));
		_statusLabel?.AddThemeColorOverride("font_color", new Color(0.84f, 0.89f, 0.92f));
		_tutorialHintLabel.AddThemeColorOverride("font_color", new Color(0.88f, 0.93f, 0.95f));
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
			label.AddThemeColorOverride("font_color", new Color(0.72f, 0.81f, 0.85f, 0.95f));
			label.AddThemeFontSizeOverride("font_size", 11);
		}
		_tutorialHintLabel.Text = "Q/E rotates. F3 shows debug tools.";
	}

	private void ConfigureInHandPreviewViewport()
	{
		_inHandPreviewViewport.Size = new Vector2I(120, 120);
		_inHandPreviewViewport.TransparentBg = false;
		_inHandPreviewViewport.OwnWorld3D = true;
		_inHandPreviewViewport.RenderTargetUpdateMode = SubViewport.UpdateMode.Always;
		_inHandPreviewViewport.Msaa3D = Viewport.Msaa.Msaa2X;
		_inHandPreviewViewport.RenderTargetClearMode = SubViewport.ClearMode.Always;
		_tilePreviewRect.Texture = _inHandPreviewViewport.GetTexture();
		_tilePreviewRect.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;
		_inHandPreviewCamera.Position = new Vector3(0f, 3f, 2f);
		_inHandPreviewCamera.Fov = 40f;
		_inHandPreviewCamera.LookAt(Vector3.Zero, Vector3.Up);
		_inHandPreviewLight.Rotation = new Vector3(Mathf.DegToRad(-45f), Mathf.DegToRad(45f), 0f);
		_inHandPreviewLight.LightEnergy = 1.2f;
		var previewEnvironment = _inHandPreviewViewport.GetNode<WorldEnvironment>("PreviewWorldEnvironment");
		previewEnvironment.Environment ??= new Godot.Environment();
		previewEnvironment.Environment.BackgroundMode = Godot.Environment.BGMode.Color;
		previewEnvironment.Environment.BackgroundColor = new Color(0.165f, 0.247f, 0.290f);

		_nextPreviewViewport.Size = new Vector2I(72, 72);
		_nextPreviewViewport.TransparentBg = false;
		_nextPreviewViewport.OwnWorld3D = true;
		_nextPreviewViewport.RenderTargetUpdateMode = SubViewport.UpdateMode.Always;
		_nextPreviewViewport.Msaa3D = Viewport.Msaa.Msaa2X;
		_nextPreviewViewport.RenderTargetClearMode = SubViewport.ClearMode.Always;
		_nextPreviewRect.Texture = _nextPreviewViewport.GetTexture();
		_nextPreviewRect.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;
		_nextPreviewCamera.Position = new Vector3(0f, 3f, 2f);
		_nextPreviewCamera.Fov = 40f;
		_nextPreviewCamera.LookAt(Vector3.Zero, Vector3.Up);
		_nextPreviewLight.Rotation = new Vector3(Mathf.DegToRad(-45f), Mathf.DegToRad(45f), 0f);
		_nextPreviewLight.LightEnergy = 1.2f;
		var nextEnvironment = _nextPreviewViewport.GetNode<WorldEnvironment>("PreviewWorldEnvironment");
		nextEnvironment.Environment ??= new Godot.Environment();
		nextEnvironment.Environment.BackgroundMode = Godot.Environment.BGMode.Color;
		nextEnvironment.Environment.BackgroundColor = new Color(0.165f, 0.247f, 0.290f);
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
		var tileDeckWidth = Mathf.Clamp(size.X * 0.21f, 250f, 340f);
		var tileDeckHeight = Mathf.Clamp(size.Y * 0.30f, 220f, 320f);
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

	private static StandardMaterial3D CreatePreviewOverlayMaterial(Color color)
	{
		return new StandardMaterial3D
		{
			Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
			ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
			AlbedoColor = color,
			RimEnabled = true,
			Rim = 0.55f,
			RimTint = 0.85f
		};
	}

	private void ConfigureMinimalHud()
	{
		GetNode<PanelContainer>("CanvasLayer/HUDRoot/TopLeftPanel/CurrencyRow/HighScoreChip").Visible = true;
		GetNode<Label>("CanvasLayer/HUDRoot/TileDeckPanel/DeckVBox/DeckTitle").Text = "In Hand";
		_unlockedTilesLabel.Visible = false;
		_lastMessageLabel.Visible = false;
		if (_statusLabel is not null)
			_statusLabel.Visible = false;
		_tutorialHintPanel.Visible = false;
		_submitScoreButton.Visible = false;
		if (HasNode("CanvasLayer/HUDRoot/BottomCenter"))
			GetNode<Control>("CanvasLayer/HUDRoot/BottomCenter").Visible = false;
		if (HasNode("CanvasLayer/HUDRoot/HintArrow"))
			GetNode<Control>("CanvasLayer/HUDRoot/HintArrow").Visible = false;
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
		EnsureVisibleSlots();
	}

	private void ConfigureWorldEnvironmentAndOcean()
	{
		foreach (var child in _decorRoot.GetChildren())
			child.QueueFree();

		_waterSurface = null!;
		ConfigureWorldSky();
		BuildWaterSurface();
	}

	private void BuildWaterSurface()
	{
		var plane = new PlaneMesh
		{
			Size = new Vector2(1800f, 1800f),
			SubdivideWidth = 256,
			SubdivideDepth = 256
		};
		var material = new ShaderMaterial();
		var shader = GD.Load<Shader>("res://shaders/ocean_water.gdshader");
		if (shader is not null)
			material.Shader = shader;

		material.SetShaderParameter("deep_color", new Color(0.12f, 0.40f, 0.54f, 1.0f));
		material.SetShaderParameter("shallow_color", new Color(0.18f, 0.55f, 0.66f, 1.0f));
		material.SetShaderParameter("wave_height", 0.04f);
		material.SetShaderParameter("wave_speed", 0.40f);

		_waterSurface = new MeshInstance3D
		{
			Name = "WaterSurface",
			Mesh = plane,
			Position = new Vector3(0f, -0.05f, 0f),
			// PlaneMesh is already horizontal in world space; rotating it causes a vertical "wall".
			Rotation = Vector3.Zero,
			MaterialOverride = material,
			CastShadow = GeometryInstance3D.ShadowCastingSetting.Off
		};
		_decorRoot.AddChild(_waterSurface);
	}

	private void ConfigureWorldSky()
	{
		// DO NOT add any backdrop/cylinder/plane mesh here.
		// Background is handled entirely by ProceduralSkyMaterial.
		// See issue history — backdrop mesh causes a visible wall.
		var worldEnvironment = GetNodeOrNull<WorldEnvironment>("WorldEnvironment");
		if (worldEnvironment is null)
			return;

		var env = new Godot.Environment();
		var skyMaterial = new ProceduralSkyMaterial
		{
			SkyTopColor = new Color(0.53f, 0.81f, 0.92f),
			SkyHorizonColor = new Color(0.72f, 0.88f, 0.94f),
			GroundHorizonColor = new Color(0.72f, 0.88f, 0.94f),
			GroundBottomColor = new Color(0.45f, 0.72f, 0.85f)
		};
		var sky = new Sky { SkyMaterial = skyMaterial };
		env.Sky = sky;
		env.BackgroundMode = Godot.Environment.BGMode.Sky;
		worldEnvironment.Environment = env;
	}

	private void InitializeHexMetrics()
	{
		_hexOuterRadius = HexRadius;
		_hexSpacingX = _hexOuterRadius * 1.7320508f;
		_hexSpacingZ = _hexOuterRadius * 1.5f;
		_sharedHexPrismMesh = CreateHexPrism(_hexOuterRadius, 1.0f);
	}

	private void EnforceCameraPitchClamp()
	{
		_cameraPitch = Mathf.Clamp(_cameraPitch, MinCameraPitch, MaxCameraPitch);
	}

	private static ArrayMesh CreateHexPrism(float radius, float height)
	{
		var st = new SurfaceTool();
		st.Begin(Mesh.PrimitiveType.Triangles);

		var top = new Vector3[6];
		var bottom = new Vector3[6];
		for (var i = 0; i < 6; i++)
		{
			var angle = Mathf.DegToRad(30f + (60f * i));
			var x = radius * Mathf.Cos(angle);
			var z = radius * Mathf.Sin(angle);
			top[i] = new Vector3(x, height, z);
			bottom[i] = new Vector3(x, 0f, z);
		}

		var topCenter = new Vector3(0f, height, 0f);
		for (var i = 0; i < 6; i++)
		{
			st.AddVertex(topCenter);
			st.AddVertex(top[i]);
			st.AddVertex(top[(i + 1) % 6]);
		}

		var bottomCenter = new Vector3(0f, 0f, 0f);
		for (var i = 0; i < 6; i++)
		{
			st.AddVertex(bottomCenter);
			st.AddVertex(bottom[(i + 1) % 6]);
			st.AddVertex(bottom[i]);
		}

		for (var i = 0; i < 6; i++)
		{
			var j = (i + 1) % 6;
			st.AddVertex(top[i]);
			st.AddVertex(bottom[i]);
			st.AddVertex(bottom[j]);
			st.AddVertex(top[i]);
			st.AddVertex(bottom[j]);
			st.AddVertex(top[j]);
		}

		st.GenerateNormals();
		return st.Commit();
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
		KillHoverRingTween();
		if (IsAlive(_hoverRingRoot))
			_hoverRingRoot.QueueFree();

		_hoverRingRoot = null!;
		_hoverRingMaterial = null!;

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

	private static Node3D BuildPlacementOutline()
	{
		var overlay = new Node3D();
		var mat = new StandardMaterial3D
		{
			AlbedoColor = new Color(0.48f, 0.94f, 0.92f, 0.08f),
			Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
			ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
			RimEnabled = true,
			Rim = 0.45f,
			RimTint = 0.82f
		};

		const float radius = 1.0f;
		for (var i = 0; i < 6; i++)
		{
			var angle = Mathf.Pi / 6f + (Mathf.Tau * i / 6f);
			var bar = new MeshInstance3D
			{
				Mesh = new BoxMesh { Size = new Vector3(0.54f, 0.012f, 0.04f) },
				Position = new Vector3(Mathf.Cos(angle) * radius, 0, Mathf.Sin(angle) * radius),
				Rotation = new Vector3(0, -angle, 0),
				MaterialOverride = mat,
				CastShadow = GeometryInstance3D.ShadowCastingSetting.Off
			};
			overlay.AddChild(bar);
		}

		return overlay;
	}

	private static StandardMaterial3D GetOutlineMaterial(Node3D overlay)
	{
		foreach (var child in overlay.GetChildren())
		{
			if (child is MeshInstance3D mesh && mesh.MaterialOverride is StandardMaterial3D material)
				return material;
		}

		return new StandardMaterial3D();
	}

	private void EnsureVisibleSlots()
	{
		var desiredSlots = new HashSet<int>();
		if (_session is not null)
		{
			foreach (var slotIndex in _session.State.Board.Keys)
				desiredSlots.Add(slotIndex);

			foreach (var slotIndex in GetAdjacentEmptySlots())
				desiredSlots.Add(slotIndex);
		}
		else
		{
			desiredSlots.Add(HexGrid.OriginSlotIndex);
		}

		foreach (var slotIndex in desiredSlots)
		{
			if (!_slots.ContainsKey(slotIndex))
				CreateSlotVisual(slotIndex);
		}

		foreach (var slotIndex in _slots.Keys.ToArray())
		{
			if (desiredSlots.Contains(slotIndex) || (_hoveredSlotIndex is int hovered && hovered == slotIndex))
				continue;

			var visual = _slots[slotIndex];
			if (_session is not null && _session.State.Board.ContainsKey(slotIndex))
				continue;

			visual.Root.QueueFree();
			_slots.Remove(slotIndex);
			_renderedTiles.Remove(slotIndex);
			RemoveRipplesForSlot(slotIndex);
		}
	}

	private void CreateSlotVisual(int slotIndex)
	{
		var position = GetSlotPosition(slotIndex);
		var root = new Node3D { Name = $"Slot{slotIndex}", Position = position };
		_boardRoot.AddChild(root);

		var baseHexMaterial = new StandardMaterial3D
		{
			AlbedoColor = new Color(0.12f, 0.18f, 0.22f, 0.0f),
			Roughness = 0.95f,
			Transparency = BaseMaterial3D.TransparencyEnum.Alpha
		};

		var baseHex = new MeshInstance3D
		{
			Name = "BaseHex",
			Mesh = new CylinderMesh
			{
				Height = 0.18f,
				TopRadius = _hexOuterRadius,
				BottomRadius = _hexOuterRadius * 0.98f,
				RadialSegments = 6
			},
			Rotation = new Vector3(0, Mathf.Pi / 6.0f, 0)
		};
		baseHex.MaterialOverride = baseHexMaterial;
		root.AddChild(baseHex);

		var overlay = BuildPlacementOutline();
		overlay.Name = "Overlay";
		overlay.Position = new Vector3(0, 0.16f, 0);
		root.AddChild(overlay);

		var body = new StaticBody3D { Name = "ClickBody" };
		body.SetMeta("slot_index", slotIndex);
		root.AddChild(body);

		var collision = new CollisionShape3D
		{
			Shape = new CylinderShape3D { Height = 0.45f, Radius = _hexOuterRadius * 0.92f },
			Position = new Vector3(0, 0.18f, 0)
		};
		body.AddChild(collision);

		var tileRoot = new Node3D { Name = "TileRoot", Position = new Vector3(0, 0.12f, 0) };
		root.AddChild(tileRoot);

		_slots[slotIndex] = new SlotVisual(slotIndex, root, baseHex, baseHexMaterial, overlay, tileRoot, tileRoot.Position.Y, slotIndex * 0.67f, GetOutlineMaterial(overlay));
	}

	private void StartNewSession(ulong seed)
	{
		PersistHighScoreIfNeeded();
		_eventSink.Clear();
		_rotationQuarterTurns = 0;
		_comboCount = 0;
		_bestCombo = 0;
		_deckRemaining = DefaultDeckSize;
		_queuedTileType = null;
		_queuedTileSeed = 0UL;
		_currentHandSeed = 0UL;
		_currentHandPattern = SectorPattern.Pure(TileType.Plains);
		_queuedTilePattern = SectorPattern.Pure(TileType.Plains);
		_nextPreviewTileType = null;
		_hoveredSlotIndex = null;
		_autoDrawPending = false;
		_autoDrawTimer = 0f;
		_menuVisible = false;
		_menuOverlay.Visible = false;
		_comboLabel.Visible = false;
		_cameraPanOffset = Vector3.Zero;
		AnimateHoverRing(false);
		_session = new GameSession(
			GameState.CreateInitial(seed),
			_eventSink,
			_highScoreRepository,
			ResolveHighScorePath());
		ApplyThemeForSession(seed, showSplash: true);
		_session.State.SetUnlockedTiles(GameState.AllTileTypes);
		_session.State.Board[HexGrid.OriginSlotIndex] = new PlacedTile(TileType.Plains, 0);
		_session.State.LastMessage = "Starter plains tile placed.";

		_renderedTiles.Clear();
		ClearAllTileVisuals();
		ClearFloatingPopups();
		_perfWarned = false;
		BuildBoardVisuals();
		PrepareNextQueuedTile();
		QueueAutoDraw(0.0f);
		UpdateAutoDraw(0.0f);
		SetStatus($"Started new session with seed {seed}. Theme: {GetThemeDisplayName(_activeTheme)}.");
		Render();
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
			ApplyThemeForSession(loaded.RngState, showSplash: false);
			_renderedTiles.Clear();
			ClearAllTileVisuals();
			ClearFloatingPopups();
			_perfWarned = false;
			_hoveredSlotIndex = null;
			_comboCount = 0;
			_autoDrawPending = false;
			_autoDrawTimer = 0f;
			_queuedTileType = null;
			_queuedTileSeed = 0UL;
			_currentHandSeed = loaded.RngState;
			_currentHandPattern = _session.State.CurrentTile is TileType currentTile
				? TileSectorGenerator.Generate(currentTile, _currentHandSeed, _session.State.ThemeContext)
				: SectorPattern.Pure(TileType.Plains);
			_queuedTilePattern = SectorPattern.Pure(TileType.Plains);
			_nextPreviewTileType = null;
			_deckRemaining = Math.Max(0, DefaultDeckSize - Math.Max(0, loaded.Board.Count - 1));
			_cameraPanOffset = Vector3.Zero;
			AnimateHoverRing(false);
			BuildBoardVisuals();
			PrepareNextQueuedTile();
			if (_session.State.CurrentTile is null)
			{
				QueueAutoDraw(0.0f);
				UpdateAutoDraw(0.0f);
			}
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
		if (_session is not null && _session.State.Score > _highScoreValue)
		{
			_highScoreValue = _session.State.Score;
			_highScoreRepository.SaveHighScore(ResolveHighScorePath(), _highScoreValue);
		}

		if (_highScoreRepository.TryLoadHighScore(ResolveHighScorePath(), out var highScore, out var error))
		{
			_highScoreValue = highScore;
			_highScoreLabel.Text = $"🏝️ {_session?.State.Board.Count ?? 0}   🃏 {_deckRemaining} left";
			if (!string.IsNullOrWhiteSpace(error))
				SetStatus(error);
		}
		else
		{
			_highScoreValue = 0;
			_highScoreLabel.Text = $"🏝️ {_session?.State.Board.Count ?? 0}   🃏 {_deckRemaining} left";
			SetStatus(error);
		}
	}

	private void PersistHighScoreIfNeeded()
	{
		if (_session is null)
			return;

		var score = _session.State.Score;
		if (score <= _highScoreValue)
			return;

		_highScoreRepository.SaveHighScore(ResolveHighScorePath(), score);
		_highScoreValue = score;
	}

	private void HandleRunComplete()
	{
		PersistHighScoreIfNeeded();
		_menuOverlay.Visible = true;
		_menuVisible = true;
		_resumeButton.Visible = false;
		_overlayNewGameButton.Text = "Play Again";
		_quitButton.Text = "Main Menu";
		_menuPanel.GetNode<Label>("MenuVBox/MenuTitle").Text = $"Run Complete\nScore: {_session.State.Score}\nCoins: {_session.State.Coins}\nTiles: {_session.State.Board.Count}\nBest Combo: x{Math.Max(1, _bestCombo)}";
		_menuOverlay.Modulate = Colors.White;
	}

	private void Issue(IGameCommand command)
	{
		var previousCoins = _session.State.Coins;
		var previousBoardCount = _session.State.Board.Count;
		try
		{
			_session.IssueCommand(command);
			if (command is PlaceTileCommand placed)
			{
				if (_session.State.Board.Count > previousBoardCount)
				{
					TriggerPlacementFocus(placed.SlotIndex);
					var earnedCoins = _session.State.Coins - previousCoins;
					if (earnedCoins > 0)
					{
						_comboCount += 1;
						ShowPlacementCoinsPopup(placed.SlotIndex, earnedCoins);
					}
					else
					{
						_comboCount = 0;
					}
					PlayPlacementSound(placed.SlotIndex, _session.State.Board[placed.SlotIndex].TileType);
					SpawnPlacementBurst(placed.SlotIndex);
					_bestCombo = Math.Max(_bestCombo, _comboCount);
					_deckRemaining = Math.Max(0, _deckRemaining - 1);
					if (_comboCount >= 6)
					{
						_deckRemaining += 5;
						ShowDeckRewardPopup(5);
						PlayStreakRewardSound(placed.SlotIndex, 1.18f);
					}
					else if (_comboCount >= 3)
					{
						_deckRemaining += 3;
						ShowDeckRewardPopup(3);
						PlayStreakRewardSound(placed.SlotIndex, 1.06f);
					}
					if (_deckRemaining > 0)
					{
						QueueAutoDraw(0.3f);
					}
					else
					{
						HandleRunComplete();
					}
					_rotationQuarterTurns = 0;
				}
				else
				{
					TriggerInvalidPlacementFeedback();
				}
			}
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
	}

	private void QueueAutoDraw(float delaySeconds)
	{
		if (_session is null || _session.State.CurrentTile is not null)
			return;

		_autoDrawPending = true;
		_autoDrawTimer = delaySeconds;
	}

	private void UpdateAutoDraw(float delta)
	{
		if (!_autoDrawPending || _session is null)
			return;

		_autoDrawTimer = Mathf.Max(0f, _autoDrawTimer - delta);
		if (_autoDrawTimer > 0f)
			return;

		_autoDrawPending = false;
		GrantWeightedTileToHand();
	}

	private void AnimateTileReceived()
	{
		if (_tileDeckPanel is null)
			return;

		_tileDeckPanel.Scale = new Vector2(0.9f, 0.9f);
		var tween = CreateTween();
		tween.SetEase(Tween.EaseType.Out);
		tween.SetTrans(Tween.TransitionType.Quad);
		tween.TweenProperty(_tileDeckPanel, "scale", new Vector2(1.05f, 1.05f), 0.10);
		tween.TweenProperty(_tileDeckPanel, "scale", Vector2.One, 0.10);
	}

	private void ShowPlacementCoinsPopup(int slotIndex, long amount)
	{
		var label = new Label3D
		{
			Text = $"+{amount} coins",
			Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
			FontSize = 36,
			OutlineSize = 6,
			Modulate = new Color(1.0f, 0.84f, 0.0f, 0.0f),
			Position = GetSlotPosition(slotIndex) + new Vector3(0, 1.45f, 0)
		};
		_boardRoot.AddChild(label);
		_floatingPopups.Add(new FloatingPopup(label, label.Position, label.Position + new Vector3(0, 0.5f, 0), 1.2f));
	}

	private void ShowComboFeedback(int comboCount, int slotIndex)
	{
		_comboLabel.Text = $"COMBO x{comboCount}";
		_comboLabel.Visible = true;
		_comboLabel.Scale = new Vector2(0.85f, 0.85f);
		_comboLabel.Modulate = new Color(1f, 0.84f, 0.2f, 0f);
		var tween = CreateTween();
		tween.SetEase(Tween.EaseType.Out);
		tween.SetTrans(Tween.TransitionType.Back);
		tween.TweenProperty(_comboLabel, "modulate", new Color(1f, 0.84f, 0.2f, 1f), 0.08);
		tween.Parallel().TweenProperty(_comboLabel, "scale", new Vector2(1.12f, 1.12f), 0.10);
		tween.TweenProperty(_comboLabel, "scale", Vector2.One, 0.12);
		tween.TweenInterval(0.25);
		tween.TweenProperty(_comboLabel, "modulate", new Color(1f, 0.84f, 0.2f, 0f), 0.25);
		tween.Finished += () => _comboLabel.Visible = false;
	}

	private void ShowDeckRewardPopup(int addedTiles)
	{
		ShowCenterBanner($"+{addedTiles} TILES!", new Color(1f, 0.843f, 0f, 1f), 1.20f, 0.80);
	}

	private void ShowCenterBanner(string text, Color color, float peakScale, double fadeDuration)
	{
		_comboLabel.Text = text;
		_comboLabel.Visible = true;
		_comboLabel.Scale = Vector2.Zero;
		_comboLabel.Modulate = new Color(color.R, color.G, color.B, 1f);
		var tween = CreateTween();
		tween.SetEase(Tween.EaseType.Out);
		tween.SetTrans(Tween.TransitionType.Back);
		tween.TweenProperty(_comboLabel, "scale", new Vector2(peakScale, peakScale), 0.18);
		tween.TweenProperty(_comboLabel, "scale", Vector2.One, 0.12);
		tween.TweenInterval(0.10);
		tween.TweenProperty(_comboLabel, "modulate", new Color(color.R, color.G, color.B, 0f), fadeDuration);
		tween.Finished += () => _comboLabel.Visible = false;
	}

	private void SpawnPlacementBurst(int slotIndex)
	{
		var burst = new GpuParticles3D
		{
			Position = GetSlotPosition(slotIndex) + new Vector3(0f, 0.45f, 0f),
			Amount = 18,
			Lifetime = 0.45,
			OneShot = true,
			Explosiveness = 1.0f,
			Preprocess = 0.0f,
			DrawPass1 = new QuadMesh { Size = new Vector2(0.06f, 0.06f) },
			VisibilityAabb = new Aabb(new Vector3(-1, -1, -1), new Vector3(2, 2, 2))
		};
		burst.ProcessMaterial = new ParticleProcessMaterial
		{
			EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Sphere,
			EmissionSphereRadius = 0.08f,
			InitialVelocityMin = 0.6f,
			InitialVelocityMax = 1.2f,
			ScaleMin = 0.5f,
			ScaleMax = 1.0f,
			Color = new Color(1.0f, 0.85f, 0.35f, 1.0f),
			Gravity = new Vector3(0f, -1.4f, 0f)
		};
		_boardRoot.AddChild(burst);
		burst.Emitting = true;
		var tween = CreateTween();
		tween.TweenInterval(1.0);
		tween.Finished += () =>
		{
			if (IsAlive(burst))
				burst.QueueFree();
		};
	}

	private void PlayPlacementSound(int slotIndex, TileType tileType)
	{
		var player = new AudioStreamPlayer3D
		{
			Position = GetSlotPosition(slotIndex) + new Vector3(0f, 0.25f, 0f),
			VolumeDb = -8f,
			UnitSize = 8f,
			MaxDistance = 24f
		};
		var stream = new AudioStreamGenerator
		{
			MixRate = 44100,
			BufferLength = 0.2f
		};
		player.Stream = stream;
		_boardRoot.AddChild(player);
		player.Play();
		if (player.GetStreamPlayback() is AudioStreamGeneratorPlayback playback)
		{
			var pitch = tileType switch
			{
				TileType.Lake or TileType.Volcano => 0.8f,
				TileType.River or TileType.Pond or TileType.Coral => 1.2f,
				_ => 1.0f
			};
			var phase = 0.0;
			var frames = (int)(stream.MixRate * 0.1);
			for (var i = 0; i < frames; i++)
			{
				var t = i / (float)frames;
				var freq = Mathf.Lerp(200f * pitch, 80f * pitch, t);
				phase += Math.Tau * freq / stream.MixRate;
				var env = 1.0f - t;
				var sample = (float)(Math.Sin(phase) * env * 0.25f);
				playback.PushFrame(new Vector2(sample, sample));
			}
		}

		var cleanup = CreateTween();
		cleanup.TweenInterval(0.3);
		cleanup.Finished += () =>
		{
			if (IsAlive(player))
				player.QueueFree();
		};
	}

	private void PlayStreakRewardSound(int slotIndex, float pitchScale)
	{
		var player = new AudioStreamPlayer3D
		{
			Position = GetSlotPosition(slotIndex) + new Vector3(0f, 0.45f, 0f),
			VolumeDb = -6f,
			UnitSize = 10f,
			MaxDistance = 28f
		};
		var stream = new AudioStreamGenerator
		{
			MixRate = 44100,
			BufferLength = 0.25f
		};
		player.Stream = stream;
		_boardRoot.AddChild(player);
		player.Play();
		if (player.GetStreamPlayback() is AudioStreamGeneratorPlayback playback)
		{
			var phase = 0.0;
			var frames = (int)(stream.MixRate * 0.14);
			for (var i = 0; i < frames; i++)
			{
				var t = i / (float)frames;
				var freq = Mathf.Lerp(440f * pitchScale, 760f * pitchScale, t);
				phase += Math.Tau * freq / stream.MixRate;
				var env = 1.0f - t;
				var sample = (float)(Math.Sin(phase) * env * 0.18f);
				playback.PushFrame(new Vector2(sample, sample));
			}
		}

		var cleanup = CreateTween();
		cleanup.TweenInterval(0.35);
		cleanup.Finished += () =>
		{
			if (IsAlive(player))
				player.QueueFree();
		};
	}

	private void UpdateFloatingPopups(float delta)
	{
		for (var i = _floatingPopups.Count - 1; i >= 0; i--)
		{
			var popup = _floatingPopups[i];
			if (!IsAlive(popup.Label))
			{
				_floatingPopups.RemoveAt(i);
				continue;
			}

			popup.Elapsed += delta;
			var t = Mathf.Clamp(popup.Elapsed / popup.Duration, 0f, 1f);
			popup.Label.Position = popup.StartPosition.Lerp(popup.EndPosition, t);
			var alpha = t < 0.15f ? (t / 0.15f) : (1.0f - ((t - 0.15f) / 0.85f));
			popup.Label.Modulate = new Color(1.0f, 0.84f, 0.0f, Mathf.Clamp(alpha, 0f, 1f));

			if (t < 1f)
				continue;

			popup.Label.QueueFree();
			_floatingPopups.RemoveAt(i);
		}
	}

	private void GrantWeightedTileToHand()
	{
		if (_session is null || _session.State.CurrentTile is not null || _deckRemaining <= 0)
			return;

		if (_queuedTileType is null)
			PrepareNextQueuedTile();

		if (_queuedTileType is null)
			return;

		_session.State.RngState = _queuedTileSeed;
		_session.State.RngStep += 1;
		_session.State.CurrentTile = _queuedTileType.Value;
		_currentHandSeed = _queuedTileSeed;
		_currentHandPattern = _queuedTilePattern;
		_session.State.LastMessage = $"Received {GetTileDisplayName(_queuedTileType.Value)}.";
		_eventSink.Publish(new UiNoteEvent($"Tile received: {GetTileDisplayName(_queuedTileType.Value)}"));
		PrepareNextQueuedTile();
		AnimateTileReceived();
		RefreshGhostTile();
		Render();
	}

	private void PrepareNextQueuedTile()
	{
		if (_session is null || _deckRemaining <= 0)
		{
			_queuedTileType = null;
			return;
		}

		var rng = NextPresentationRng(_session.State.RngState);
		var unlocked = _session.State.UnlockedTiles.ToHashSet();
		var pool = GetThemeTilePool(_activeTheme)
			.Where(x => unlocked.Contains(x.TileType))
			.ToArray();

		if (pool.Length == 0)
		{
			_queuedTileType = null;
			return;
		}

		var totalWeight = pool.Sum(x => x.Weight);
		var roll = (int)(rng % (ulong)totalWeight);
		var cumulative = 0;
		var selected = pool[0].TileType;
		foreach (var entry in pool)
		{
			cumulative += entry.Weight;
			if (roll < cumulative)
			{
				selected = entry.TileType;
				break;
			}
		}

		GD.Print($"Theme pool [{GetThemeDisplayName(_activeTheme)}]: {string.Join(", ", pool.Select(x => $"{GetTileDisplayName(x.TileType)}={x.Weight}"))}");
		_queuedTileSeed = rng;
		_queuedTileType = selected;
		_queuedTilePattern = TileSectorGenerator.Generate(selected, _queuedTileSeed, ToThemeContext(_activeTheme));
	}

	private void ApplyThemeForSession(ulong seed, bool showSplash)
	{
		_activeTheme = (WorldTheme)(NextPresentationRng(seed ^ 0x9E3779B97F4A7C15UL) % 3UL);
		if (_session is not null)
			_session.State.ThemeContext = ToThemeContext(_activeTheme);
		if (showSplash)
			ShowCenterBanner($"{GetThemeEmoji(_activeTheme)} {GetThemeDisplayName(_activeTheme)}", new Color(0.95f, 0.86f, 0.44f, 1f), 1.12f, 0.65);
	}

	private static TileSectorGenerator.ThemeContext ToThemeContext(WorldTheme theme) => theme switch
	{
		WorldTheme.Temperate => TileSectorGenerator.ThemeContext.Temperate,
		WorldTheme.Tropical => TileSectorGenerator.ThemeContext.Tropical,
		_ => TileSectorGenerator.ThemeContext.Frontier
	};

	private static (TileType TileType, int Weight)[] GetThemeTilePool(WorldTheme theme) => theme switch
	{
		WorldTheme.Temperate =>
		[
			(TileType.Plains, 14),
			(TileType.Woods, 14),
			(TileType.Meadow, 14),
			(TileType.Village, 14),
			(TileType.Pond, 14),
			(TileType.Ruins, 10),
			(TileType.Lake, 10),
			(TileType.River, 10)
		],
		WorldTheme.Tropical =>
		[
			(TileType.River, 14),
			(TileType.Coral, 14),
			(TileType.Woods, 14),
			(TileType.Volcano, 14),
			(TileType.Savanna, 14),
			(TileType.Pond, 10),
			(TileType.Swamp, 10)
		],
		_ =>
		[
			(TileType.Desert, 14),
			(TileType.Tundra, 14),
			(TileType.Snow, 14),
			(TileType.Volcano, 14),
			(TileType.Mushroom, 14),
			(TileType.Swamp, 10),
			(TileType.Lake, 10)
		]
	};

	private static string GetThemeDisplayName(WorldTheme theme) => theme switch
	{
		WorldTheme.Temperate => "Temperate World",
		WorldTheme.Tropical => "Tropical World",
		_ => "Frontier World"
	};

	private static string GetThemeEmoji(WorldTheme theme) => theme switch
	{
		WorldTheme.Temperate => "🌿",
		WorldTheme.Tropical => "🌊",
		_ => "🦂"
	};

	private static ulong NextPresentationRng(ulong value)
	{
		value ^= value >> 12;
		value ^= value << 25;
		value ^= value >> 27;
		return value * 2685821657736338717UL;
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
		var showHover = nextHovered is int slotIndex &&
			_session.State.CurrentTile is not null &&
			!_session.State.Board.ContainsKey(slotIndex);
		AnimateHoverRing(showHover);
		RenderBoard();
	}

	private void AnimateHoverRing(bool visible)
	{
		KillHoverRingTween();
		if (!IsAlive(_hoverRingRoot))
			return;

		if (visible && _hoveredSlotIndex is int slot && _slots.TryGetValue(slot, out var slotVisual))
		{
			var canPlace = CanPlaceCurrentTile(slot, out _);
			_hoverRingBaseColor = canPlace
				? new Color(0.28f, 0.94f, 0.84f, 0.0f)
				: new Color(0.96f, 0.36f, 0.38f, 0.0f);
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

	private bool CanPlaceCurrentTile(int slotIndex, out PlacementRejectionReason? rejectionReason)
	{
		rejectionReason = null;
		if (_session.State.CurrentTile is null)
		{
			rejectionReason = PlacementRejectionReason.NoTileInHand;
			return false;
		}

		if (_session.State.Board.ContainsKey(slotIndex))
		{
			rejectionReason = PlacementRejectionReason.SlotOccupied;
			return false;
		}

		if (_session.State.Board.Count == 0)
			return true;

		var hasNeighbor = HexGrid.GetNeighborSlots(slotIndex).Any(_session.State.Board.ContainsKey);
		if (!hasNeighbor)
			rejectionReason = PlacementRejectionReason.IncompatibleAdjacent;

		return hasNeighbor;
	}

	private void Render()
	{
		RenderBoard();

		var state = _session.State;
		_coinsLabel.Text = $"🪙 {state.Coins}";
		_scoreLabel.Text = $"⭐ {state.Score}  Best: {_highScoreValue}";
		_highScoreLabel.Text = $"# {Math.Max(0, state.Board.Count)}   🃏 {_deckRemaining} left";
		var streakText = _comboCount >= 2 ? $"  🔥 {_comboCount}" : string.Empty;
		_highScoreLabel.Text = $"# {Math.Max(0, state.Board.Count)}{streakText}   🃏 {_deckRemaining} left";
		_currentTileLabel.Text = state.CurrentTile is null
			? "No tile in hand"
			: GetTileDisplayName(state.CurrentTile.Value);
		_rotationLabel.Text = $"Rotation {_rotationQuarterTurns * 60}\u00B0  (Q/E)";
		_eventLog.Text = string.Join('\n', (_minimalPlayMode ? _eventSink.RecentLines.TakeLast(4) : _eventSink.RecentLines).Select(l => $"\u2022 {CompactText(l, 64)}"));
		SyncInHandPreview(state.CurrentTile);
		SyncNextPreview();
		_tutorialHintPanel.Visible = false;
		_eventTitleLabel.Text = _utilityPanelVisible
			? $"Recent  ({_lastMeshInstanceCount} meshes / {_lastNodeCount} nodes)"
			: "Recent";
		UpdateActionButtonStates(state);
	}

	private void UpdateActionButtonStates(GameState state)
	{
		_submitScoreButton.Disabled = state.Score <= 0;
		_submitScoreButton.Text = state.Score > 0 ? $"Submit Score ({state.Score})" : "Submit Score";
		_submitScoreButton.Modulate = _submitScoreButton.Disabled ? new Color(0.86f, 0.88f, 0.92f) : new Color(0.92f, 0.94f, 1.0f);
	}

	private void RenderBoard()
	{
		EnsureVisibleSlots();
		foreach (var (slotIndex, visual) in _slots)
		{
			var isHovered = _hoveredSlotIndex == slotIndex;
			var occupied = _session.State.Board.TryGetValue(slotIndex, out var placed);
			visual.BaseMaterial.AlbedoColor = occupied
				? new Color(0.16f, 0.22f, 0.25f, 0.06f)
				: new Color(0.16f, 0.22f, 0.25f, 0.0f);
			visual.BaseMaterial.EmissionEnabled = isHovered;
			visual.BaseMaterial.Emission = new Color(0.48f, 0.90f, 0.88f);
			visual.BaseMaterial.EmissionEnergyMultiplier = isHovered ? 0.08f : 0.0f;

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

			var normalizedRotation = ((placed.RotationQuarterTurns % 6) + 6) % 6;
			var signature = BuildVisualSignature(slotIndex, placed.TileType, normalizedRotation);
			if (_renderedTiles.TryGetValue(slotIndex, out var existing) && existing.Equals(signature))
				continue;

			_renderedTiles[slotIndex] = signature;
			RebuildPlacedTileVisual(slotIndex, visual.TileRoot, placed.TileType, normalizedRotation, animate: true);
		}

		if (_hoveredSlotIndex is int hovered && _slots.TryGetValue(hovered, out var hoveredVisual))
		{
			if (_session.State.CurrentTile is not null && !_session.State.Board.ContainsKey(hovered))
			{
				_hoverRingBaseColor = CanPlaceCurrentTile(hovered, out _)
					? new Color(0.28f, 0.94f, 0.84f, 0.0f)
					: new Color(0.96f, 0.36f, 0.38f, 0.0f);
				HoverRingAlpha = HoverRingAlpha;
			}
			if (IsAlive(_hoverRingRoot))
				_hoverRingRoot.Position = hoveredVisual.Root.Position;
		}

		UpdatePreviewVisual();
	}

	private void KillHoverRingTween()
	{
		if (IsAlive(_hoverRingTween))
			_hoverRingTween.Kill();

		_hoverRingTween = null!;
	}

	private static bool IsAlive(GodotObject obj)
	{
		return obj is not null && GodotObject.IsInstanceValid(obj);
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

	private void UpdatePlacementOutlines()
	{
		if (_session is null)
			return;

		var validAdjacents = GetAdjacentEmptySlots();
		var holdingTile = _session.State.CurrentTile is not null;
		var pulse = 1.0f + (Mathf.Sin((_time / 1.5f) * Mathf.Tau) * 0.03f);

		foreach (var pair in _slots)
		{
			var slotIndex = pair.Key;
			var visual = pair.Value;
			var occupied = _session.State.Board.ContainsKey(slotIndex);
			var show = holdingTile && !occupied && validAdjacents.Contains(slotIndex);
			visual.Overlay.Visible = show;
			visual.Overlay.Scale = show ? new Vector3(pulse, 1f, pulse) : Vector3.One;

			if (!show)
				continue;

			var isHovered = _hoveredSlotIndex == slotIndex;
			var outlineColor = isHovered
				? new Color(0.54f, 0.98f, 0.96f, 0.12f)
				: new Color(0.40f, 0.88f, 0.86f, 0.08f);
			visual.OverlayMaterial.AlbedoColor = outlineColor;
		}
	}

	private HashSet<int> GetAdjacentEmptySlots()
	{
		var result = new HashSet<int>();
		if (_session is null)
			return result;

		foreach (var slotIndex in _session.State.Board.Keys)
		{
			foreach (var neighborSlot in HexGrid.GetNeighborSlots(slotIndex))
				AddAdjacentCandidate(result, neighborSlot);
		}

		return result;
	}

	private void AddAdjacentCandidate(HashSet<int> result, int slotIndex)
	{
		if (_session.State.Board.ContainsKey(slotIndex))
			return;

		result.Add(slotIndex);
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
			_session.State.CurrentTile is null)
		{
			_previewRoot.Visible = false;
			return;
		}

		var hoveredSlot = _hoveredSlotIndex.Value;
		var slotOccupied = _session.State.Board.ContainsKey(hoveredSlot);
		var canPlace = CanPlaceCurrentTile(hoveredSlot, out _);
		if (slotOccupied)
		{
			_previewRoot.Visible = false;
			return;
		}

		_previewRoot.Visible = true;
		_previewRoot.Position = slot.Root.Position + new Vector3(0, 0.02f, 0);

		if (!_hasPreviewVisual || _previewTileType != _session.State.CurrentTile || !IsAlive(_previewVisual))
		{
			if (_hasPreviewVisual && IsAlive(_previewVisual))
				_previewVisual.QueueFree();

			_previewTileType = _session.State.CurrentTile;
			var realTile = CreateTileDiorama(_previewTileType.Value, GetPreviewSectorPattern(), unchecked((int)(_currentHandSeed & 0x7FFFFFFF)), preview: false);
			_previewVisual = CreateGhostTile(realTile, canPlace);
			_previewRoot.AddChild(_previewVisual);
			_hasPreviewVisual = true;
		}

		_previewVisual.Rotation = new Vector3(0, _rotationQuarterTurns * Mathf.Pi / 3.0f, 0);
		UpdateGhostAppearance(_previewVisual, canPlace);
	}

	private void RefreshGhostTile()
	{
		if (_hasPreviewVisual && IsAlive(_previewVisual))
			_previewVisual.QueueFree();

		_previewVisual = null!;
		_hasPreviewVisual = false;
		_previewTileType = null;
	}

	private void RebuildPlacedTileVisual(int slotIndex, Node3D tileRoot, TileType tileType, int rotationQuarterTurns, bool animate)
	{
		RemoveRipplesForSlot(slotIndex);
		ClearTileHolder(tileRoot);
		var sectorPattern = _session.State.Board.TryGetValue(slotIndex, out var placedTile)
			? placedTile.ResolvedSectorPattern
			: SectorPattern.Pure(tileType);
		var diorama = CreateTileDiorama(tileType, sectorPattern, slotIndex, preview: false);
		diorama.Rotation = new Vector3(0, rotationQuarterTurns * Mathf.Pi / 3.0f, 0);
		tileRoot.AddChild(diorama);

		if (animate)
		{
			diorama.Scale = Vector3.One;
			diorama.Position = new Vector3(0f, 2.0f, 0f);
			var dropTween = CreateTween();
			dropTween.SetEase(Tween.EaseType.Out);
			dropTween.SetTrans(Tween.TransitionType.Bounce);
			dropTween.TweenProperty(diorama, "position", Vector3.Zero, 0.25);

			var punchTween = CreateTween();
			punchTween.SetEase(Tween.EaseType.Out);
			punchTween.SetTrans(Tween.TransitionType.Quad);
			punchTween.TweenInterval(0.03);
			punchTween.TweenProperty(diorama, "scale", new Vector3(1.15f, 1.15f, 1.15f), 0.07);
			punchTween.TweenProperty(diorama, "scale", Vector3.One, 0.08);
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

	private void ClearFloatingPopups()
	{
		foreach (var popup in _floatingPopups)
		{
			if (IsAlive(popup.Label))
				popup.Label.QueueFree();
		}

		_floatingPopups.Clear();
	}

	private Node3D CreateTileDiorama(TileType tileType, SectorPattern sectorPattern, int visualSeed, bool preview)
	{
		var root = new Node3D { Name = $"{tileType}Diorama" };
		if (!preview)
			root.AddChild(CreateWaterShadowNode());
		root.AddChild(CreateBaseOcclusionNode(tileType));
		var surfaceRoot = new Node3D { Name = "SurfaceRoot" };
		surfaceRoot.Rotation = new Vector3(
			Mathf.DegToRad(DeterministicJitter(visualSeed, 5200 + (int)tileType) * 2.0f),
			0f,
			Mathf.DegToRad(DeterministicJitter(visualSeed, 5300 + (int)tileType) * 2.0f));
		root.AddChild(surfaceRoot);
		surfaceRoot.AddChild(CreatePlatformMeshNode(tileType, preview));
		surfaceRoot.AddChild(CreateSectorOverlayRoot(sectorPattern, tileType, preview));
		var contentRoot = new Node3D { Name = "VisualContent" };
		surfaceRoot.AddChild(contentRoot);
		BuildSectorProps(contentRoot, tileType, sectorPattern, visualSeed, preview);

		return root;
	}

	private Node3D TryInstantiateTileScene(TileType tileType, int visualSeed)
	{
		if (!_tileScenePaths.TryGetValue(tileType, out var path))
			return null;

		if (!_tileScenes.TryGetValue(tileType, out var scene))
		{
			scene = GD.Load<PackedScene>(path);
			if (scene is null)
				return null;

			_tileScenes[tileType] = scene;
		}

		if (scene.Instantiate() is not Node3D instance)
			return null;

		StripLegacyPlatformNodes(instance);
		var scaleJitter = 1.0f + (DeterministicJitter(visualSeed, 14000 + (int)tileType) * 0.02f);
		instance.Scale = new Vector3(scaleJitter, scaleJitter, scaleJitter);
		instance.Rotation = new Vector3(0, DeterministicJitter(visualSeed, 14100 + (int)tileType) * 0.08f, 0);
		return instance;
	}

	private static void StripLegacyPlatformNodes(Node3D instance)
	{
		instance.GetNodeOrNull<Node>("Cliff")?.Free();
		instance.GetNodeOrNull<Node>("Top")?.Free();
		instance.GetNodeOrNull<Node>("Shore")?.Free();
		instance.GetNodeOrNull<Node>("GrassPatch")?.Free();
	}

	private static (float BaseY, float TopY) GetTilePlatformProfile(TileType tileType) => tileType switch
	{
		TileType.Lake or TileType.Volcano => (0f, 0.5f),
		TileType.Pond => (0f, 0.12f),
		TileType.Coral => (0f, 0.15f),
		_ => (0f, 0.28f)
	};

	private MeshInstance3D CreatePlatformMeshNode(TileType tileType, bool preview)
	{
		var (baseY, topY) = GetTilePlatformProfile(tileType);
		var height = Mathf.Max(0.05f, topY - baseY);
		return new MeshInstance3D
		{
			Name = "PlatformMesh",
			Mesh = _sharedHexPrismMesh,
			Position = new Vector3(0f, baseY, 0f),
			Scale = new Vector3(1f, height, 1f),
			MaterialOverride = CreatePlatformMaterial(tileType, preview),
			CastShadow = GeometryInstance3D.ShadowCastingSetting.On
		};
	}

	private MeshInstance3D CreateWaterShadowNode()
	{
		var material = new StandardMaterial3D
		{
			AlbedoColor = new Color(0.039f, 0.125f, 0.188f, 0.18f),
			Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
			ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
			CullMode = BaseMaterial3D.CullModeEnum.Disabled
		};
		return new MeshInstance3D
		{
			Name = "WaterShadow",
			Mesh = new CylinderMesh
			{
				TopRadius = _hexOuterRadius * 0.90f,
				BottomRadius = _hexOuterRadius * 0.90f,
				Height = 0.01f,
				RadialSegments = 6
			},
			Position = new Vector3(0f, -0.03f, 0f),
			Rotation = new Vector3(0f, Mathf.Pi / 6.0f, 0f),
			MaterialOverride = material,
			CastShadow = GeometryInstance3D.ShadowCastingSetting.Off
		};
	}

	private MeshInstance3D CreateBaseOcclusionNode(TileType tileType)
	{
		var color = GetTileBevelColor(tileType).Darkened(0.60f);
		return new MeshInstance3D
		{
			Name = "BaseOcclusion",
			Mesh = _sharedHexPrismMesh,
			Position = new Vector3(0f, 0f, 0f),
			Scale = new Vector3(1.01f, tileType == TileType.Plains ? 0.06f : 0.08f, 1.01f),
			MaterialOverride = new StandardMaterial3D
			{
				AlbedoColor = new Color(color.R, color.G, color.B, 0.95f),
				Roughness = 1.0f,
				CullMode = BaseMaterial3D.CullModeEnum.Disabled
			},
			CastShadow = GeometryInstance3D.ShadowCastingSetting.Off
		};
	}

	private Node3D CreateSectorOverlayRoot(SectorPattern sectorPattern, TileType tileType, bool preview)
	{
		var (_, topY) = GetTilePlatformProfile(tileType);
		var root = new Node3D { Name = "SectorOverlayRoot", Position = new Vector3(0f, topY + 0.002f, 0f) };
		for (var i = 0; i < 6; i++)
		{
			var sector = new MeshInstance3D
			{
				Name = $"Sector{i}",
				Mesh = sectorPattern[i] == TileType.Pond
					? CreateRecessedSectorTopMesh(i, 0.12f)
					: CreateSectorTopMesh(i, sectorPattern[i]),
				MaterialOverride = CreateSectorMaterial(sectorPattern[i], preview),
				CastShadow = GeometryInstance3D.ShadowCastingSetting.Off
			};
			root.AddChild(sector);
		}

		for (var i = 0; i < 6; i++)
		{
			var left = sectorPattern[i];
			var right = sectorPattern[(i + 1) % 6];
			if (left == right || left == TileType.Pond || right == TileType.Pond)
				continue;

			var blendStrip = new MeshInstance3D
			{
				Name = $"Blend{i}",
				Mesh = CreateSectorBlendStripMesh(i),
				MaterialOverride = CreateSectorBlendMaterial(left, right, preview),
				CastShadow = GeometryInstance3D.ShadowCastingSetting.Off
			};
			root.AddChild(blendStrip);
		}

		return root;
	}

	private static Node3D GetVisualContentRoot(Node3D diorama)
	{
		return diorama.GetNodeOrNull<Node3D>("VisualContent") ?? diorama;
	}

	private void SyncInHandPreview(TileType? tileType)
	{
		if (tileType is null)
		{
			if (IsAlive(_inHandPreviewTile))
				_inHandPreviewTile.QueueFree();
			_inHandPreviewTile = null!;
			_inHandPreviewTileType = null;
			return;
		}

		var seed = _session.State.RngState;
		if (tileType == _inHandPreviewTileType && seed == _inHandPreviewSeed && IsAlive(_inHandPreviewTile))
			return;

		if (IsAlive(_inHandPreviewTile))
			_inHandPreviewTile.QueueFree();

		_inHandPreviewSeed = seed;
		_inHandPreviewTileType = tileType;
		_inHandPreviewTile = CreateTileDiorama(tileType.Value, GetPreviewSectorPattern(), unchecked((int)(_currentHandSeed & 0x7FFFFFFF)), preview: false);
		_inHandPreviewTile.Position = Vector3.Zero;
		_inHandPreviewTile.Rotation = Vector3.Zero;
		_inHandPreviewRoot.AddChild(_inHandPreviewTile);
	}

	private void SyncNextPreview()
	{
		if (_queuedTileType is null)
		{
			if (IsAlive(_nextPreviewTile))
				_nextPreviewTile.QueueFree();
			_nextPreviewTile = null!;
			_nextPreviewTileType = null;
			_nextLabel.Text = "Next";
			return;
		}

		if (_nextPreviewTileType == _queuedTileType && IsAlive(_nextPreviewTile))
			return;

		if (IsAlive(_nextPreviewTile))
			_nextPreviewTile.QueueFree();

		_nextPreviewTileType = _queuedTileType;
		var seed = unchecked((int)(_queuedTileSeed & 0x7FFFFFFF));
		_nextPreviewTile = CreateTileDiorama(_queuedTileType.Value, _queuedTilePattern, seed, preview: false);
		_nextPreviewTile.Position = Vector3.Zero;
		_nextPreviewRoot.AddChild(_nextPreviewTile);
		_nextLabel.Text = $"Next: {GetTileDisplayName(_queuedTileType.Value)}";
	}

	private void UpdateInHandPreview(float delta)
	{
		if (IsAlive(_inHandPreviewTile))
			_inHandPreviewTile.Rotation = new Vector3(0f, _inHandPreviewTile.Rotation.Y + (delta * Mathf.DegToRad(20f)), 0f);
		if (IsAlive(_nextPreviewTile))
			_nextPreviewTile.Rotation = new Vector3(0f, _nextPreviewTile.Rotation.Y + (delta * Mathf.DegToRad(20f)), 0f);
	}

	private void BuildSectorProps(Node3D root, TileType dominantTileType, SectorPattern sectorPattern, int seed, bool preview)
	{
		var (_, topY) = GetTilePlatformProfile(dominantTileType);
		var regions = GetSectorRegions(sectorPattern);
		var primaryRegion = regions
			.OrderByDescending(r => r.Terrain == dominantTileType ? r.Sectors.Length + 10 : r.Sectors.Length)
			.FirstOrDefault();
		if (primaryRegion.Terrain == TileType.Pond)
		{
			var fallbackRegion = regions
				.Where(r => r.Terrain != TileType.Pond)
				.OrderByDescending(r => r.Sectors.Length)
				.FirstOrDefault();
			if (fallbackRegion.Sectors is not null && fallbackRegion.Sectors.Length > 0)
				primaryRegion = fallbackRegion;
		}
		if (primaryRegion.Sectors is null || primaryRegion.Sectors.Length == 0)
			return;

		var anchor = GetRegionAnchor(primaryRegion, topY, seed, 0);
		WarnIfNearCenter(anchor, $"{primaryRegion.Terrain}PrimaryRegion");
		var outwardYaw = Mathf.Atan2(anchor.Z, anchor.X);
		var clusterRoot = new Node3D { Name = $"{primaryRegion.Terrain}PrimaryRegion" };
		root.AddChild(clusterRoot);

		switch (primaryRegion.Terrain)
		{
			case TileType.Plains:
				AddPlainsProps(clusterRoot, anchor, outwardYaw, seed, 0, primaryRegion, dominantTileType, preview);
				break;
			case TileType.Woods:
				AddForestProps(clusterRoot, anchor, outwardYaw, seed, 0, preview);
				break;
			case TileType.Lake:
				AddMountainProps(clusterRoot, anchor, preview, volcanic: false);
				break;
			case TileType.River:
				AddBeachProps(clusterRoot, anchor, outwardYaw, preview);
				break;
			case TileType.Village:
				AddCityProps(clusterRoot, anchor, outwardYaw, seed, 0, preview);
				break;
			case TileType.Meadow:
				AddFarmlandProps(clusterRoot, anchor, outwardYaw, preview);
				break;
			case TileType.Desert:
				AddDesertProps(clusterRoot, anchor, outwardYaw, seed, 0, preview);
				break;
			case TileType.Snow:
				AddSnowProps(clusterRoot, anchor, outwardYaw, seed, 0, preview);
				break;
			case TileType.Volcano:
				AddMountainProps(clusterRoot, anchor, preview, volcanic: true);
				break;
			case TileType.Pond:
				// Lake sectors are rendered as recessed terrain directly in the top mesh; no lake props.
				break;
			case TileType.Ruins:
				AddRuinsProps(clusterRoot, anchor, outwardYaw, seed, 0, preview);
				break;
			case TileType.Swamp:
				AddSwampProps(clusterRoot, anchor, outwardYaw, seed, 0, preview);
				break;
			case TileType.Savanna:
				AddSavannaProps(clusterRoot, anchor, outwardYaw, seed, 0, preview);
				break;
			case TileType.Tundra:
				AddTundraProps(clusterRoot, anchor, outwardYaw, seed, 0, preview);
				break;
			case TileType.Coral:
				AddCoralProps(clusterRoot, anchor, outwardYaw, seed, 0, preview);
				break;
			case TileType.Mushroom:
				AddMushroomProps(clusterRoot, anchor, outwardYaw, seed, 0, preview);
				break;
		}

		if (dominantTileType != TileType.Plains)
		{
			var plainsSecondary = regions
				.Where(r => r.Terrain == TileType.Plains)
				.OrderByDescending(r => r.Sectors.Length)
				.FirstOrDefault();
			if (plainsSecondary.Sectors is { Length: > 0 and <= 2 })
			{
				var plainsRoot = new Node3D { Name = "PlainsSecondaryRegion" };
				root.AddChild(plainsRoot);
				var plainsAnchor = GetRegionAnchor(plainsSecondary, topY, seed, 71);
				AddPlainsMinorProps(plainsRoot, plainsAnchor, seed, preview);
			}
		}
	}

	private List<SectorRegion> GetSectorRegions(SectorPattern pattern)
	{
		var regions = new List<SectorRegion>();
		var sectors = pattern.ToArray();
		var currentTerrain = sectors[0];
		var currentSectors = new List<int> { 0 };

		for (var i = 1; i < sectors.Length; i++)
		{
			if (sectors[i] == currentTerrain)
			{
				currentSectors.Add(i);
				continue;
			}

			regions.Add(new SectorRegion(currentTerrain, currentSectors.ToArray()));
			currentTerrain = sectors[i];
			currentSectors = new List<int> { i };
		}

		regions.Add(new SectorRegion(currentTerrain, currentSectors.ToArray()));
		if (regions.Count > 1 && regions[0].Terrain == regions[^1].Terrain)
		{
			var merged = regions[^1].Sectors.Concat(regions[0].Sectors).ToArray();
			regions[0] = new SectorRegion(regions[0].Terrain, merged);
			regions.RemoveAt(regions.Count - 1);
		}

		return regions;
	}

	private Vector3 GetRegionAnchor(SectorRegion region, float topY, int seed, int regionIndex)
	{
		var sinSum = 0f;
		var cosSum = 0f;
		foreach (var sectorIndex in region.Sectors)
		{
			var angle = Mathf.DegToRad(60f * sectorIndex + 30f);
			sinSum += Mathf.Sin(angle);
			cosSum += Mathf.Cos(angle);
		}

		float centroidAngle;
		if (region.Sectors.Length == 6)
		{
			var pseudoSector = Mathf.Abs(seed + regionIndex * 17) % 6;
			centroidAngle = Mathf.DegToRad(60f * pseudoSector + 30f);
		}
		else
		{
			centroidAngle = Mathf.Atan2(sinSum, cosSum);
		}

		var distance = region.Sectors.Length switch
		{
			1 => _hexOuterRadius * 0.52f,
			2 => _hexOuterRadius * 0.42f,
			3 => _hexOuterRadius * 0.35f,
			_ => _hexOuterRadius * 0.42f
		};
		return new Vector3(Mathf.Cos(centroidAngle) * distance, topY, Mathf.Sin(centroidAngle) * distance);
	}

	private Mesh CreateSectorTopMesh(int sectorIndex, TileType terrain)
	{
		var st = new SurfaceTool();
		st.Begin(Mesh.PrimitiveType.Triangles);
		var start = Mathf.DegToRad((60f * sectorIndex) - 30f);
		var end = start + Mathf.DegToRad(60f);
		var center = new Vector3(0f, 0f, 0f);
		var a = new Vector3(_hexOuterRadius * Mathf.Cos(start), 0f, _hexOuterRadius * Mathf.Sin(start));
		var b = new Vector3(_hexOuterRadius * Mathf.Cos(end), 0f, _hexOuterRadius * Mathf.Sin(end));
		var centerColor = Colors.White;
		var edgeColor = Colors.White;
		if (terrain == TileType.Plains)
			edgeColor = new Color(0.92f, 0.92f, 0.92f, 1f);
		st.SetColor(centerColor);
		st.AddVertex(center);
		st.SetColor(edgeColor);
		st.AddVertex(a);
		st.SetColor(edgeColor);
		st.AddVertex(b);
		st.GenerateNormals();
		return st.Commit();
	}

	private Mesh CreateRecessedSectorTopMesh(int sectorIndex, float centerDepth)
	{
		var st = new SurfaceTool();
		st.Begin(Mesh.PrimitiveType.Triangles);
		var start = Mathf.DegToRad((60f * sectorIndex) - 30f);
		var end = start + Mathf.DegToRad(60f);
		var center = new Vector3(0f, -centerDepth, 0f);
		var a = new Vector3(_hexOuterRadius * Mathf.Cos(start), 0f, _hexOuterRadius * Mathf.Sin(start));
		var b = new Vector3(_hexOuterRadius * Mathf.Cos(end), 0f, _hexOuterRadius * Mathf.Sin(end));
		st.AddVertex(center);
		st.AddVertex(a);
		st.AddVertex(b);
		st.GenerateNormals();
		return st.Commit();
	}

	private Mesh CreateSectorBlendStripMesh(int boundaryIndex)
	{
		var st = new SurfaceTool();
		st.Begin(Mesh.PrimitiveType.Triangles);
		var boundaryAngle = Mathf.DegToRad((60f * boundaryIndex) + 30f);
		var tangent = new Vector2(-Mathf.Sin(boundaryAngle), Mathf.Cos(boundaryAngle));
		var radial = new Vector2(Mathf.Cos(boundaryAngle), Mathf.Sin(boundaryAngle));
		var halfWidth = _hexOuterRadius * 0.025f;
		var inner = _hexOuterRadius * 0.16f;
		var outer = _hexOuterRadius * 0.92f;
		var a = new Vector3(radial.X * inner + tangent.X * halfWidth, 0f, radial.Y * inner + tangent.Y * halfWidth);
		var b = new Vector3(radial.X * inner - tangent.X * halfWidth, 0f, radial.Y * inner - tangent.Y * halfWidth);
		var c = new Vector3(radial.X * outer - tangent.X * halfWidth, 0f, radial.Y * outer - tangent.Y * halfWidth);
		var d = new Vector3(radial.X * outer + tangent.X * halfWidth, 0f, radial.Y * outer + tangent.Y * halfWidth);
		st.AddVertex(a);
		st.AddVertex(b);
		st.AddVertex(c);
		st.AddVertex(a);
		st.AddVertex(c);
		st.AddVertex(d);
		st.GenerateNormals();
		return st.Commit();
	}

	private StandardMaterial3D CreateSectorMaterial(TileType terrain, bool preview)
	{
		var color = GetTileSwatchColor(terrain);
		return new StandardMaterial3D
		{
			AlbedoColor = preview ? new Color(color.R, color.G, color.B, 0.60f) : color,
			Roughness = terrain == TileType.Plains ? 0.85f : 0.82f,
			Metallic = 0.0f,
			VertexColorUseAsAlbedo = true,
			Transparency = preview ? BaseMaterial3D.TransparencyEnum.Alpha : BaseMaterial3D.TransparencyEnum.Disabled,
			CullMode = BaseMaterial3D.CullModeEnum.Disabled
		};
	}

	private StandardMaterial3D CreateSectorBlendMaterial(TileType left, TileType right, bool preview)
	{
		var color = GetTileSwatchColor(left).Lerp(GetTileSwatchColor(right), 0.5f);
		return new StandardMaterial3D
		{
			AlbedoColor = preview ? new Color(color.R, color.G, color.B, 0.42f) : new Color(color.R, color.G, color.B, 0.85f),
			Roughness = 0.84f,
			Transparency = preview ? BaseMaterial3D.TransparencyEnum.Alpha : BaseMaterial3D.TransparencyEnum.Disabled,
			CullMode = BaseMaterial3D.CullModeEnum.Disabled
		};
	}

	private StandardMaterial3D CreatePlatformMaterial(TileType tileType, bool preview)
	{
		var color = GetTileBevelColor(tileType);
		return new StandardMaterial3D
		{
			AlbedoColor = preview ? new Color(color.R, color.G, color.B, 0.52f) : color,
			Roughness = 0.96f,
			Transparency = preview ? BaseMaterial3D.TransparencyEnum.Alpha : BaseMaterial3D.TransparencyEnum.Disabled,
			RimEnabled = true,
			Rim = 0.30f,
			RimTint = 0.50f
		};
	}

	private SectorPattern GetPreviewSectorPattern()
	{
		if (_session?.State.CurrentTile is not TileType)
			return SectorPattern.Pure(TileType.Plains);

		return _currentHandPattern;
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
		Mesh depthMesh = isLake
			? new CylinderMesh { Height = 0.008f, TopRadius = sizeHint.X * 0.92f, BottomRadius = sizeHint.X * 0.92f, RadialSegments = 20 }
			: new BoxMesh { Size = new Vector3(sizeHint.X * 0.90f, 0.008f, sizeHint.Z * 0.94f) };

		var baseWater = CreateColoredMesh(baseMesh, new Vector3(0, y, 0), isLake ? new Color(0.12f, 0.38f, 0.46f) : new Color(0.14f, 0.42f, 0.56f), preview);
		baseWater.Rotation = new Vector3(0, yaw, 0);
		ConfigureWaterMaterial((StandardMaterial3D)baseWater.MaterialOverride, seed, isLake, preview, highlightLayer: false);
		baseWater.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
		root.AddChild(baseWater);

		var depthLayer = CreateColoredMesh(depthMesh, new Vector3(0, y + 0.004f, 0), isLake ? new Color(0.08f, 0.30f, 0.40f) : new Color(0.10f, 0.34f, 0.48f), preview);
		depthLayer.Rotation = new Vector3(0, yaw - 0.02f, 0);
		ConfigureWaterMaterial((StandardMaterial3D)depthLayer.MaterialOverride, seed + 11, isLake, preview, highlightLayer: false);
		if (depthLayer.MaterialOverride is StandardMaterial3D depthMat)
			depthMat.AlbedoColor = new Color(depthMat.AlbedoColor.R, depthMat.AlbedoColor.G, depthMat.AlbedoColor.B, preview ? 0.10f : 0.22f);
		depthLayer.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
		root.AddChild(depthLayer);

		var highlight = CreateColoredMesh(highlightMesh, new Vector3(0, y + 0.008f, 0), isLake ? new Color(0.28f, 0.69f, 0.78f) : new Color(0.25f, 0.65f, 0.80f), preview);
		highlight.Rotation = new Vector3(0, yaw + 0.03f, 0);
		ConfigureWaterMaterial((StandardMaterial3D)highlight.MaterialOverride, seed + 1, isLake, preview, highlightLayer: true);
		highlight.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
		root.AddChild(highlight);

		BuildWaterShelf(root, seed, preview, isLake, yaw, sizeHint);
		BuildWetShoreSegments(root, seed, preview, isLake, yaw, isLake ? 0.50f : 0.56f);
		if (!preview)
			BuildWaterRipples(root, seed, isLake, yaw, isLake ? 0.42f : 0.46f, y + 0.014f);
	}

	private void BuildWaterShelf(Node3D root, int seed, bool preview, bool isLake, float yaw, Vector3 sizeHint)
	{
		var shelfColor = isLake
			? new Color(0.20f, 0.36f, 0.30f)
			: new Color(0.18f, 0.34f, 0.28f);

		Mesh shelfMesh = isLake
			? new CylinderMesh
			{
				Height = 0.010f,
				TopRadius = sizeHint.X * 1.06f,
				BottomRadius = sizeHint.X * 1.06f,
				RadialSegments = 20
			}
			: new BoxMesh { Size = new Vector3(sizeHint.X * 1.08f, 0.010f, sizeHint.Z * 1.05f) };

		var shelf = CreateColoredMesh(shelfMesh, new Vector3(0, isLake ? 0.146f : 0.142f, 0), shelfColor, preview);
		shelf.Rotation = new Vector3(0, yaw, 0);
		if (shelf.MaterialOverride is StandardMaterial3D shelfMat)
		{
			shelfMat.Metallic = 0.0f;
			shelfMat.Roughness = 0.94f;
			ApplyMaterialJitter(shelfMat, seed, isLake ? 6120 : 3120, 0.03f, 0.92f);
		}
		root.AddChild(shelf);
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

	private static bool IsWater(TileType? tileType) => tileType is TileType.River or TileType.Lake or TileType.Pond;

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

		baseColor = HarmonizeTerrainColor(tileType, baseColor, visualSeed, composition);
		var tint = 0.95f + (Mathf.Abs(Mathf.Sin(visualSeed * 0.173f)) * 0.05f);
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
		{
			ApplyPropMaterialProfile(mat, color);
			ApplyMaterialJitter(mat, (int)(position.X * 1000f + position.Z * 1000f), 8000, 0.06f, 1.0f);
		}
		return meshInstance;
	}

	private void AddPlainsProps(Node3D root, Vector3 anchor, float yaw, int seed, int regionIndex, SectorRegion plainsRegion, TileType dominantTileType, bool preview)
	{
		var rng = MakeVisualRng(seed, 18000 + regionIndex);
		var sectorCount = plainsRegion.Sectors.Length;
		if (sectorCount >= 6)
		{
			// Single Plains tile: fixed cozy layout with tiny scatter.
			var treeCount = rng.RandiRange(2, 3);
			var positions = new[]
			{
				PolarOnTop(30f, 0.32f),
				PolarOnTop(150f, 0.28f),
				PolarOnTop(270f, 0.30f)
			};
			for (var i = 0; i < treeCount; i++)
				AddPlainsRoundTree(root, positions[i] + RegionScatter(0f, rng, 0.04f), rng, preview);
			AddPlainsRockCluster(root, PolarOnTop(90f, 0.20f) + RegionScatter(0f, rng, 0.04f), rng, preview);
			AddPlainsGrassTufts(root, rng, 6, preview);
			return;
		}

		var center = anchor;
		if (sectorCount <= 2 && dominantTileType != TileType.Plains)
		{
			AddPlainsRoundTree(root, center, rng, preview, small: true);
			AddPlainsGrassTufts(root, rng, 2, preview, center);
			return;
		}

		var treeMin = sectorCount >= 4 ? 2 : 2;
		var treeMax = sectorCount >= 4 ? 3 : 2;
		var treeCountRegion = rng.RandiRange(treeMin, treeMax);
		for (var i = 0; i < treeCountRegion; i++)
			AddPlainsRoundTree(root, center + RegionScatter(yaw, rng, 0.10f), rng, preview);
		AddPlainsRockCluster(root, center + RegionScatter(yaw, rng, 0.10f), rng, preview);
		AddPlainsGrassTufts(root, rng, rng.RandiRange(4, 6), preview, center);
	}

	private void AddPlainsMinorProps(Node3D root, Vector3 anchor, int seed, bool preview)
	{
		var rng = MakeVisualRng(seed, 18141);
		AddPlainsRoundTree(root, anchor + RegionScatter(0f, rng, 0.06f), rng, preview, small: true);
	}

	private void AddPlainsRoundTree(Node3D root, Vector3 position, RandomNumberGenerator rng, bool preview, bool small = false)
	{
		var scale = small ? rng.RandfRange(0.70f, 0.86f) : rng.RandfRange(0.85f, 1.15f);
		var treeRoot = new Node3D
		{
			Position = position,
			Rotation = new Vector3(0f, rng.RandfRange(0f, Mathf.Tau), 0f),
			Scale = Vector3.One * scale
		};
		root.AddChild(treeRoot);

		var trunk = CreateColoredMesh(
			new CylinderMesh { TopRadius = 0.03f, BottomRadius = 0.03f, Height = 0.18f, RadialSegments = 10 },
			new Vector3(0f, 0.09f, 0f),
			new Color(0.42f, 0.24f, 0.10f),
			preview);
		treeRoot.AddChild(trunk);

		treeRoot.AddChild(CreateColoredMesh(new SphereMesh { Radius = 0.18f, Height = 0.18f, RadialSegments = 12, Rings = 8 }, new Vector3(0f, 0.22f, 0f), new Color(0.353f, 0.667f, 0.227f), preview));
		treeRoot.AddChild(CreateColoredMesh(new SphereMesh { Radius = 0.14f, Height = 0.14f, RadialSegments = 12, Rings = 8 }, new Vector3(0f, 0.32f, 0f), new Color(0.290f, 0.604f, 0.165f), preview));
		treeRoot.AddChild(CreateColoredMesh(new SphereMesh { Radius = 0.09f, Height = 0.09f, RadialSegments = 12, Rings = 8 }, new Vector3(0f, 0.40f, 0f), new Color(0.227f, 0.541f, 0.102f), preview));
	}

	private void AddPlainsRockCluster(Node3D root, Vector3 position, RandomNumberGenerator rng, bool preview)
	{
		var sizes = new[] { 0.06f, 0.04f, 0.035f };
		var offsets = new[]
		{
			new Vector3(-0.03f, 0f, 0.0f),
			new Vector3(0.03f, 0.0f, -0.02f),
			new Vector3(0.0f, 0.0f, 0.03f)
		};
		for (var i = 0; i < sizes.Length; i++)
		{
			var rock = CreateColoredMesh(
				new SphereMesh { Radius = sizes[i], Height = sizes[i] * 0.9f, RadialSegments = 8, Rings = 6 },
				position + offsets[i] + new Vector3(0f, rng.RandfRange(0.0f, 0.01f), 0f),
				new Color(0.604f, 0.541f, 0.471f),
				preview);
			rock.Scale = new Vector3(1f, 0.5f, 1f);
			root.AddChild(rock);
		}
	}

	private void AddPlainsGrassTufts(Node3D root, RandomNumberGenerator rng, int count, bool preview, Vector3? around = null)
	{
		for (var i = 0; i < count; i++)
		{
			var basePos = around ?? Vector3.Zero;
			var random = around is null
				? RandomTopPoint(rng, _hexOuterRadius * 0.70f)
				: basePos + RegionScatter(0f, rng, 0.12f);
			var tuft = CreateColoredMesh(
				new PlaneMesh { Size = new Vector2(0.06f, 0.06f) },
				new Vector3(random.X, 0.281f, random.Z),
				i % 2 == 0 ? new Color(0.420f, 0.749f, 0.267f) : new Color(0.541f, 0.831f, 0.333f),
				preview);
			tuft.Rotation = new Vector3(Mathf.DegToRad(-90f), rng.RandfRange(0f, Mathf.Tau), 0f);
			root.AddChild(tuft);
		}
	}

	private static Vector3 PolarOnTop(float degrees, float distance)
	{
		var a = Mathf.DegToRad(degrees);
		return new Vector3(Mathf.Cos(a) * distance, 0f, Mathf.Sin(a) * distance);
	}

	private void AddForestProps(Node3D root, Vector3 anchor, float yaw, int seed, int regionIndex, bool preview)
	{
		var rng = MakeVisualRng(seed, 18100 + regionIndex);
		var count = rng.RandiRange(4, 6);
		for (var i = 0; i < count; i++)
			AddLayeredTree(root, anchor + RegionScatter(yaw, rng, 0.18f), rng.RandfRange(0.18f, 0.26f), rng.RandfRange(0.42f, 0.62f), new Color(0.18f, 0.35f, 0.21f).Lerp(new Color(0.26f, 0.43f, 0.23f), rng.Randf()), preview, rounded: false);

		var log = CreateColoredMesh(
			new CylinderMesh { Height = 0.18f, TopRadius = 0.025f, BottomRadius = 0.03f, RadialSegments = 8 },
			anchor + RegionScatter(yaw, rng, 0.08f) + new Vector3(0f, 0.04f, 0f),
			new Color(0.42f, 0.29f, 0.18f),
			preview);
		log.Rotation = new Vector3(Mathf.Pi * 0.5f, yaw + 0.4f, 0f);
		root.AddChild(log);
	}

	private void AddMountainProps(Node3D root, Vector3 anchor, bool preview, bool volcanic)
	{
		var peak = CreateColoredMesh(
			new CylinderMesh { Height = volcanic ? 0.80f : 0.70f, TopRadius = volcanic ? 0.10f : 0.03f, BottomRadius = volcanic ? 0.26f : 0.24f, RadialSegments = 8 },
			anchor + new Vector3(0f, volcanic ? 0.40f : 0.35f, 0f),
			volcanic ? new Color(0.23f, 0.13f, 0.09f) : new Color(0.55f, 0.49f, 0.43f),
			preview);
		root.AddChild(peak);

		var cap = CreateColoredMesh(
			new SphereMesh { Radius = volcanic ? 0.08f : 0.10f, Height = volcanic ? 0.03f : 0.06f, RadialSegments = 8, Rings = 5 },
			anchor + new Vector3(0f, volcanic ? 0.78f : 0.68f, 0f),
			volcanic ? new Color(1.00f, 0.27f, 0.0f) : new Color(0.94f, 0.96f, 0.98f),
			preview);
		if (volcanic && cap.MaterialOverride is StandardMaterial3D lavaMat)
		{
			lavaMat.EmissionEnabled = true;
			lavaMat.Emission = new Color(1.0f, 0.30f, 0.02f);
			lavaMat.EmissionEnergyMultiplier = 1.5f;
		}
		root.AddChild(cap);

		if (!preview)
		{
			for (var i = 0; i < 3; i++)
			{
				var rock = CreateColoredMesh(
					new BoxMesh { Size = new Vector3(0.10f + i * 0.02f, 0.08f + i * 0.02f, 0.09f + i * 0.02f) },
					anchor + new Vector3((i - 1) * 0.14f, 0.08f + i * 0.03f, 0.12f - i * 0.08f),
					volcanic ? new Color(0.28f, 0.17f, 0.13f) : new Color(0.46f, 0.42f, 0.39f),
					preview);
				rock.Rotation = new Vector3(Mathf.DegToRad(8f * i), Mathf.DegToRad(18f * i), Mathf.DegToRad(-6f * i));
				root.AddChild(rock);
			}
		}
	}

	private void AddBeachProps(Node3D root, Vector3 anchor, float yaw, bool preview)
	{
		var palm = new Node3D { Position = anchor };
		root.AddChild(palm);
		var trunk = CreateColoredMesh(
			new CylinderMesh { Height = 0.24f, TopRadius = 0.02f, BottomRadius = 0.032f, RadialSegments = 8 },
			new Vector3(0f, 0.12f, 0f),
			new Color(0.51f, 0.37f, 0.22f),
			preview);
		trunk.Rotation = new Vector3(Mathf.DegToRad(-14f), 0f, Mathf.DegToRad(10f));
		palm.AddChild(trunk);
		for (var i = -1; i <= 1; i++)
		{
			var leaf = CreateColoredMesh(new PlaneMesh { Size = new Vector2(0.20f, 0.08f) }, new Vector3(0f, 0.24f, 0f), new Color(0.22f, 0.48f, 0.25f), preview);
			leaf.Rotation = new Vector3(Mathf.DegToRad(-18f), yaw + (i * 0.8f), 0f);
			if (leaf.MaterialOverride is StandardMaterial3D mat)
				mat.CullMode = BaseMaterial3D.CullModeEnum.Disabled;
			palm.AddChild(leaf);
		}

		for (var i = 0; i < 3; i++)
		{
			var shell = CreateColoredMesh(
				new SphereMesh { Radius = 0.025f, Height = 0.012f, RadialSegments = 8, Rings = 4 },
				anchor + new Vector3(0.10f - i * 0.05f, 0.01f, -0.02f + i * 0.04f),
				new Color(0.91f, 0.88f, 0.80f),
				preview);
			root.AddChild(shell);
		}
	}

	private void AddCityProps(Node3D root, Vector3 anchor, float yaw, int seed, int regionIndex, bool preview)
	{
		var rng = MakeVisualRng(seed, 18200 + regionIndex);
		var road = CreateColoredMesh(new BoxMesh { Size = new Vector3(0.32f, 0.01f, 0.10f) }, anchor + new Vector3(0f, 0.005f, 0f), new Color(0.32f, 0.33f, 0.35f), preview);
		road.Rotation = new Vector3(0f, yaw, 0f);
		root.AddChild(road);

		var buildingCount = rng.RandiRange(4, 7);
		for (var i = 0; i < buildingCount; i++)
		{
			var footprint = (i % 3) switch
			{
				0 => new Vector3(0.10f, rng.RandfRange(0.20f, 0.42f), 0.15f),
				1 => new Vector3(0.12f, rng.RandfRange(0.18f, 0.50f), 0.12f),
				_ => new Vector3(0.08f, rng.RandfRange(0.24f, 0.60f), 0.20f)
			};
			var pos = anchor + RegionScatter(yaw, rng, 0.12f);
			var box = CreateColoredMesh(
				new BoxMesh { Size = footprint },
				pos + new Vector3(0f, footprint.Y * 0.5f, 0f),
				(i % 3) switch
				{
					0 => new Color(0.91f, 0.84f, 0.69f),
					1 => new Color(0.78f, 0.72f, 0.60f),
					_ => new Color(0.83f, 0.77f, 0.66f)
				},
				preview);
			root.AddChild(box);

			var roof = CreateColoredMesh(
				new BoxMesh { Size = new Vector3(footprint.X * 1.05f, 0.035f, footprint.Z * 1.05f) },
				pos + new Vector3(0f, footprint.Y + 0.018f, 0f),
				(i % 3) switch
				{
					0 => new Color(0.55f, 0.29f, 0.16f),
					1 => new Color(0.42f, 0.23f, 0.10f),
					_ => new Color(0.63f, 0.35f, 0.23f)
				},
				preview);
			root.AddChild(roof);
		}
	}

	private void AddFarmlandProps(Node3D root, Vector3 anchor, float yaw, bool preview)
	{
		for (var i = -2; i <= 3; i++)
		{
			var strip = CreateColoredMesh(
				new BoxMesh { Size = new Vector3(0.28f, 0.01f, 0.03f) },
				anchor + new Vector3(Mathf.Cos(yaw + Mathf.Pi * 0.5f) * i * 0.035f, 0.01f, Mathf.Sin(yaw + Mathf.Pi * 0.5f) * i * 0.035f),
				i % 2 == 0 ? new Color(0.46f, 0.58f, 0.26f) : new Color(0.56f, 0.42f, 0.20f),
				preview);
			strip.Rotation = new Vector3(0f, yaw, 0f);
			root.AddChild(strip);
		}

		var barn = CreateColoredMesh(new BoxMesh { Size = new Vector3(0.14f, 0.12f, 0.11f) }, anchor + new Vector3(-0.06f, 0.06f, -0.04f), new Color(0.78f, 0.29f, 0.16f), preview);
		root.AddChild(barn);
		var roof = CreateColoredMesh(new BoxMesh { Size = new Vector3(0.16f, 0.04f, 0.13f) }, anchor + new Vector3(-0.06f, 0.14f, -0.04f), new Color(0.35f, 0.19f, 0.06f), preview);
		roof.Rotation = new Vector3(0f, yaw, Mathf.DegToRad(12f));
		root.AddChild(roof);

		var bale = CreateColoredMesh(
			new CylinderMesh { Height = 0.10f, TopRadius = 0.04f, BottomRadius = 0.04f, RadialSegments = 10 },
			anchor + new Vector3(0.07f, 0.05f, -0.03f),
			new Color(0.74f, 0.62f, 0.28f),
			preview);
		bale.Rotation = new Vector3(Mathf.Pi * 0.5f, yaw, 0f);
		root.AddChild(bale);
	}

	private void AddDesertProps(Node3D root, Vector3 anchor, float yaw, int seed, int regionIndex, bool preview)
	{
		var rng = MakeVisualRng(seed, 18300 + regionIndex);
		var count = rng.RandiRange(1, 2);
		for (var i = 0; i < count; i++)
		{
			var cactusRoot = new Node3D { Position = anchor + RegionScatter(yaw, rng, 0.08f) };
			root.AddChild(cactusRoot);
			var stem = CreateColoredMesh(new CylinderMesh { Height = 0.18f, TopRadius = 0.025f, BottomRadius = 0.03f, RadialSegments = 8 }, new Vector3(0f, 0.09f, 0f), new Color(0.26f, 0.48f, 0.24f), preview);
			cactusRoot.AddChild(stem);
			var arm = CreateColoredMesh(new CylinderMesh { Height = 0.10f, TopRadius = 0.016f, BottomRadius = 0.018f, RadialSegments = 8 }, new Vector3(0.04f, 0.12f, 0f), new Color(0.26f, 0.48f, 0.24f), preview);
			arm.Rotation = new Vector3(0f, 0f, Mathf.DegToRad(90f));
			cactusRoot.AddChild(arm);
		}
	}

	private void AddSnowProps(Node3D root, Vector3 anchor, float yaw, int seed, int regionIndex, bool preview)
	{
		var rng = MakeVisualRng(seed, 18400 + regionIndex);
		var count = rng.RandiRange(1, 2);
		for (var i = 0; i < count; i++)
		{
			var spike = CreateColoredMesh(
				new CylinderMesh { Height = 0.22f, TopRadius = 0.012f, BottomRadius = 0.045f, RadialSegments = 6 },
				anchor + RegionScatter(yaw, rng, 0.06f) + new Vector3(0f, 0.11f, 0f),
				new Color(0.86f, 0.93f, 1.0f),
				preview);
			root.AddChild(spike);
		}
	}

	private void AddRuinsProps(Node3D root, Vector3 anchor, float yaw, int seed, int regionIndex, bool preview)
	{
		var rng = MakeVisualRng(seed, 18500 + regionIndex);
		for (var i = 0; i < 3; i++)
		{
			var pillar = CreateColoredMesh(
				new CylinderMesh { Height = 0.28f + i * 0.07f, TopRadius = 0.03f, BottomRadius = 0.04f, RadialSegments = 8 },
				anchor + RegionScatter(yaw, rng, 0.10f) + new Vector3(0f, 0.16f + i * 0.03f, 0f),
				new Color(0.62f, 0.59f, 0.52f),
				preview);
			pillar.Rotation = new Vector3(Mathf.DegToRad(rng.RandfRange(-8f, 8f)), 0f, Mathf.DegToRad(rng.RandfRange(-8f, 8f)));
			root.AddChild(pillar);
		}

		var wall = CreateColoredMesh(new BoxMesh { Size = new Vector3(0.20f, 0.11f, 0.08f) }, anchor + new Vector3(0.02f, 0.055f, -0.10f), new Color(0.58f, 0.55f, 0.49f), preview);
		wall.Rotation = new Vector3(0f, yaw + 0.5f, Mathf.DegToRad(-8f));
		root.AddChild(wall);
	}

	private void AddSwampProps(Node3D root, Vector3 anchor, float yaw, int seed, int regionIndex, bool preview)
	{
		var rng = MakeVisualRng(seed, 18600 + regionIndex);
		var count = rng.RandiRange(2, 3);
		for (var i = 0; i < count; i++)
		{
			var pos = anchor + RegionScatter(yaw, rng, 0.08f);
			var trunk = CreateColoredMesh(new CylinderMesh { Height = 0.22f, TopRadius = 0.014f, BottomRadius = 0.022f, RadialSegments = 8 }, pos + new Vector3(0f, 0.11f, 0f), new Color(0.38f, 0.31f, 0.26f), preview);
			trunk.Rotation = new Vector3(Mathf.DegToRad(rng.RandfRange(-8f, 8f)), 0f, Mathf.DegToRad(rng.RandfRange(-8f, 8f)));
			root.AddChild(trunk);
			var canopy = CreateColoredMesh(new SphereMesh { Radius = 0.05f, Height = 0.03f, RadialSegments = 8, Rings = 4 }, pos + new Vector3(0.02f, 0.21f, 0f), new Color(0.32f, 0.36f, 0.26f), preview);
			root.AddChild(canopy);
		}
	}

	private void AddSavannaProps(Node3D root, Vector3 anchor, float yaw, int seed, int regionIndex, bool preview)
	{
		var rng = MakeVisualRng(seed, 18700 + regionIndex);
		var count = rng.RandiRange(1, 2);
		for (var i = 0; i < count; i++)
		{
			var pos = anchor + RegionScatter(yaw, rng, 0.10f);
			var trunk = CreateColoredMesh(new CylinderMesh { Height = 0.18f, TopRadius = 0.015f, BottomRadius = 0.02f, RadialSegments = 8 }, pos + new Vector3(0f, 0.09f, 0f), new Color(0.45f, 0.33f, 0.21f), preview);
			root.AddChild(trunk);
			var canopy = CreateColoredMesh(new CylinderMesh { Height = 0.05f, TopRadius = 0.12f, BottomRadius = 0.14f, RadialSegments = 10 }, pos + new Vector3(0f, 0.20f, 0f), new Color(0.40f, 0.48f, 0.20f), preview);
			root.AddChild(canopy);
		}
	}

	private void AddTundraProps(Node3D root, Vector3 anchor, float yaw, int seed, int regionIndex, bool preview)
	{
		var rng = MakeVisualRng(seed, 18800 + regionIndex);
		for (var i = 0; i < 2; i++)
		{
			var rock = CreateColoredMesh(new SphereMesh { Radius = 0.05f, Height = 0.03f, RadialSegments = 8, Rings = 4 }, anchor + RegionScatter(yaw, rng, 0.06f) + new Vector3(0f, 0.015f, 0f), new Color(0.84f, 0.89f, 0.91f), preview);
			root.AddChild(rock);
		}
	}

	private void AddCoralProps(Node3D root, Vector3 anchor, float yaw, int seed, int regionIndex, bool preview)
	{
		var rng = MakeVisualRng(seed, 18900 + regionIndex);
		var palette = new[] { new Color(0.83f, 0.46f, 0.42f), new Color(0.86f, 0.54f, 0.34f), new Color(0.70f, 0.39f, 0.70f) };
		for (var i = 0; i < 3; i++)
		{
			var coralRoot = new Node3D { Position = anchor + RegionScatter(yaw, rng, 0.07f) };
			root.AddChild(coralRoot);
			var color = palette[i % palette.Length];
			for (var branch = -1; branch <= 1; branch++)
			{
				var mesh = CreateColoredMesh(new CylinderMesh { Height = 0.10f, TopRadius = 0.01f, BottomRadius = 0.018f, RadialSegments = 6 }, new Vector3(0f, 0.05f, 0f), color, preview);
				mesh.Rotation = new Vector3(Mathf.DegToRad(-15f), 0f, Mathf.DegToRad(branch * 24f));
				coralRoot.AddChild(mesh);
			}
		}
	}

	private void AddMushroomProps(Node3D root, Vector3 anchor, float yaw, int seed, int regionIndex, bool preview)
	{
		var rng = MakeVisualRng(seed, 19000 + regionIndex);
		var count = rng.RandiRange(3, 4);
		for (var i = 0; i < count; i++)
		{
			var pos = anchor + RegionScatter(yaw, rng, 0.08f);
			var stalk = CreateColoredMesh(new CylinderMesh { Height = 0.11f, TopRadius = 0.016f, BottomRadius = 0.02f, RadialSegments = 8 }, pos + new Vector3(0f, 0.055f, 0f), new Color(0.90f, 0.85f, 0.72f), preview);
			root.AddChild(stalk);
			var cap = CreateColoredMesh(new SphereMesh { Radius = i == 0 ? 0.08f : 0.06f, Height = 0.03f, RadialSegments = 8, Rings = 4 }, pos + new Vector3(0f, 0.11f, 0f), i % 2 == 0 ? new Color(0.80f, 0.27f, 1.0f) : new Color(0.82f, 0.45f, 0.92f), preview);
			cap.Scale = new Vector3(1f, 0.45f, 1f);
			if (cap.MaterialOverride is StandardMaterial3D capMat)
			{
				capMat.EmissionEnabled = true;
				capMat.Emission = new Color(0.80f, 0.27f, 1.0f);
				capMat.EmissionEnergyMultiplier = 0.4f;
			}
			root.AddChild(cap);
		}
	}

	private void AddLayeredTree(Node3D root, Vector3 anchor, float trunkHeight, float canopyHeight, Color canopyColor, bool preview, bool rounded)
	{
		var trunk = CreateColoredMesh(
			new CylinderMesh { Height = trunkHeight, TopRadius = 0.02f, BottomRadius = 0.03f, RadialSegments = 8 },
			anchor + new Vector3(0f, trunkHeight * 0.5f, 0f),
			new Color(0.42f, 0.29f, 0.18f),
			preview);
		root.AddChild(trunk);

		if (rounded)
		{
			for (var i = 0; i < 2; i++)
			{
				var canopy = CreateColoredMesh(
					new SphereMesh { Radius = 0.12f + i * 0.04f, Height = 0.11f + i * 0.03f, RadialSegments = 10, Rings = 6 },
					anchor + new Vector3(0f, trunkHeight + 0.10f + i * 0.08f, 0f),
					canopyColor.Lightened(i * 0.04f),
					preview);
				root.AddChild(canopy);
			}
		}
		else
		{
			for (var i = 0; i < 3; i++)
			{
				var layer = CreateColoredMesh(
					new CylinderMesh
					{
						Height = canopyHeight * (0.38f - i * 0.06f),
						TopRadius = 0.0f,
						BottomRadius = 0.16f + (0.06f * (2 - i)),
						RadialSegments = 8
					},
					anchor + new Vector3(0f, trunkHeight + 0.10f + i * 0.10f, 0f),
					canopyColor.Lightened(i * 0.03f),
					preview);
				root.AddChild(layer);
			}
		}
	}

	private void AddTree(Node3D root, Vector3 anchor, Color canopyColor, float canopyRadius, float trunkHeight, bool preview, bool rounded)
	{
		WarnIfNearCenter(anchor, root.Name);
		var trunk = CreateColoredMesh(
			new CylinderMesh { Height = trunkHeight, TopRadius = 0.018f, BottomRadius = 0.024f, RadialSegments = 8 },
			anchor + new Vector3(0f, trunkHeight * 0.5f, 0f),
			new Color(0.44f, 0.31f, 0.21f),
			preview);
		root.AddChild(trunk);

		Mesh canopyMesh = rounded
			? new SphereMesh { Radius = canopyRadius, Height = canopyRadius * 1.5f, RadialSegments = 10, Rings = 6 }
			: new CylinderMesh { Height = canopyRadius * 1.8f, TopRadius = 0.0f, BottomRadius = canopyRadius, RadialSegments = 8 };
		var canopy = CreateColoredMesh(canopyMesh, anchor + new Vector3(0f, trunkHeight + canopyRadius * 0.55f, 0f), canopyColor, preview);
		root.AddChild(canopy);
	}

	private static Vector3 RegionScatter(float yaw, RandomNumberGenerator rng, float radius)
	{
		var tangent = new Vector3(-Mathf.Sin(yaw), 0f, Mathf.Cos(yaw));
		var outward = new Vector3(Mathf.Cos(yaw), 0f, Mathf.Sin(yaw));
		return (tangent * rng.RandfRange(-radius, radius)) + (outward * rng.RandfRange(-radius * 0.55f, radius * 0.35f));
	}

	private static void WarnIfNearCenter(Vector3 anchor, string label)
	{
		if (new Vector2(anchor.X, anchor.Z).Length() < 0.10f)
			GD.PushWarning($"Prop anchor too close to tile center for {label}: {anchor}");
	}

	private Mesh CreateRegionSectorMesh(SectorRegion region, float radius)
	{
		var st = new SurfaceTool();
		st.Begin(Mesh.PrimitiveType.Triangles);
		for (var i = 0; i < region.Sectors.Length; i++)
		{
			var sector = region.Sectors[i];
			var start = Mathf.DegToRad((60f * sector) - 30f);
			var end = start + Mathf.DegToRad(60f);
			var a = new Vector3(radius * Mathf.Cos(start), 0f, radius * Mathf.Sin(start));
			var b = new Vector3(radius * Mathf.Cos(end), 0f, radius * Mathf.Sin(end));
			st.AddVertex(Vector3.Zero);
			st.AddVertex(a);
			st.AddVertex(b);
		}
		st.GenerateNormals();
		return st.Commit();
	}

	private void ApplyPreviewOverlay(Node node, StandardMaterial3D overlay)
	{
		if (node is MeshInstance3D mesh)
			mesh.MaterialOverlay = overlay;

		foreach (var child in node.GetChildren())
		{
			if (child is Node childNode)
				ApplyPreviewOverlay(childNode, overlay);
		}
	}

	private Node3D CreateGhostTile(Node3D realTile, bool validPlacement)
	{
		var ghost = realTile.Duplicate() as Node3D ?? realTile;
		ghost.Name = "GhostTile";
		return ghost;
	}

	private void UpdateGhostAppearance(Node3D ghost, bool validPlacement)
	{
		_ = ghost;
		_ = validPlacement;
	}

	private void ApplyPreviewTransparency(Node node, float transparency)
	{
		if (node is GeometryInstance3D geometry)
			geometry.Transparency = transparency;

		foreach (var child in node.GetChildren())
		{
			if (child is Node childNode)
				ApplyPreviewTransparency(childNode, transparency);
		}
	}

	private StandardMaterial3D GetTileMaterial(TileType tileType, bool preview, bool slab)
	{
		var target = preview ? _previewTileMaterials : _solidTileMaterials;
		if (target.TryGetValue(tileType, out var cached))
			return cached;

		var color = tileType switch
		{
			TileType.Plains => new Color(0.494f, 0.784f, 0.314f),
			TileType.Woods => new Color(0.36f, 0.60f, 0.35f),
			TileType.River => new Color(0.40f, 0.72f, 0.90f),
			TileType.Meadow => new Color(0.72f, 0.84f, 0.54f),
			TileType.Village => new Color(0.89f, 0.73f, 0.61f),
			TileType.Lake => new Color(0.37f, 0.68f, 0.78f),
			TileType.Desert => new Color(0.831f, 0.659f, 0.294f),
			TileType.Snow => new Color(0.867f, 0.910f, 0.925f),
			TileType.Volcano => new Color(0.361f, 0.227f, 0.165f),
			TileType.Pond => new Color(0.353f, 0.604f, 0.722f),
			TileType.Ruins => new Color(0.608f, 0.557f, 0.478f),
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
			AlbedoColor = preview ? new Color(color.R, color.G, color.B, 0.60f) : color,
			Roughness = 0.88f
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
		var alpha = preview ? (highlightLayer ? 0.18f : 0.14f) : (highlightLayer ? 0.26f : 0.40f);
		mat.AlbedoColor = new Color(c.R, c.G, c.B, alpha);
	}

	private Color HarmonizeTerrainColor(TileType tileType, Color baseColor, int seed, TileComposition composition)
	{
		var warmth = DeterministicJitter(seed, 7300 + (int)tileType) * 0.03f;
		var drynessShift = (composition.Dryness - 0.5f) * 0.08f;
		var waterCool = tileType is TileType.River or TileType.Lake ? composition.WaterDominance * 0.10f : 0f;

		var r = Mathf.Clamp(baseColor.R + warmth + drynessShift * 0.5f - waterCool * 0.22f, 0f, 1f);
		var g = Mathf.Clamp(baseColor.G + drynessShift * 0.18f - waterCool * 0.10f, 0f, 1f);
		var b = Mathf.Clamp(baseColor.B - warmth * 0.45f + waterCool * 0.20f, 0f, 1f);
		return new Color(r, g, b, 1f);
	}

	private static void ApplyPropMaterialProfile(StandardMaterial3D mat, Color baseColor)
	{
		var luma = (baseColor.R * 0.2126f) + (baseColor.G * 0.7152f) + (baseColor.B * 0.0722f);
		var isWaterLike = baseColor.B > baseColor.G && baseColor.B > baseColor.R;
		var isFoliageLike = baseColor.G > baseColor.R && baseColor.G > baseColor.B * 0.75f;

		if (isWaterLike)
		{
			mat.Metallic = Mathf.Max(mat.Metallic, 0.08f);
			mat.Roughness = Mathf.Min(mat.Roughness, 0.24f);
			return;
		}

		if (isFoliageLike)
		{
			mat.Metallic = 0.0f;
			mat.Roughness = Mathf.Max(mat.Roughness, 0.82f);
			return;
		}

		if (luma < 0.34f)
		{
			mat.Metallic = Mathf.Min(mat.Metallic, 0.02f);
			mat.Roughness = Mathf.Max(mat.Roughness, 0.86f);
			return;
		}

		mat.Metallic = Mathf.Min(mat.Metallic, 0.04f);
		mat.Roughness = Mathf.Clamp(mat.Roughness, 0.56f, 0.88f);
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
				new Color(0.21f, 0.30f, 0.24f),
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
		if (_waterSurface is null || _slotRipples.Count == 0)
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
		EnforceCameraPitchClamp();
		_cameraDistance = GetDefaultCameraDistanceForViewport();
		_cameraUserAdjusted = false;
		_boardCentroid = GetBoardCentroid();
		_boardCentroidSmoothed = _boardCentroid;
		_cameraPanOffset = Vector3.Zero;
		_cameraFocusOffset = _boardCentroid;
		_cameraFocusSmoothedOffset = _boardCentroid;
		_cameraPositionInitialized = false;
		UpdateCamera();
		SetStatus("Camera centered.");
	}

	private void ForceInitialCameraState()
	{
		_cameraPitch = Mathf.DegToRad(-55f);
		_cameraDistance = 8.0f;
		_boardCentroid = Vector3.Zero;
		_boardCentroidSmoothed = Vector3.Zero;
		_cameraPanOffset = Vector3.Zero;
		_cameraFocusOffset = Vector3.Zero;
		_cameraFocusSmoothedOffset = Vector3.Zero;
		_cameraPositionInitialized = false;
		UpdateCamera();
	}

	private float GetDefaultCameraDistanceForViewport()
	{
		return 8.0f;
	}

	private float GetDefaultCameraPitchForViewport()
	{
		return Mathf.DegToRad(-55f);
	}

	private void UpdateCamera()
	{
		var clampedPitch = Mathf.Clamp(_cameraPitch, MinCameraPitch, MaxCameraPitch);
		_cameraPitch = clampedPitch;
		_cameraPivot.Position = _cameraFocusSmoothedOffset;
		var dir = new Vector3(
			Mathf.Cos(clampedPitch) * Mathf.Sin(_cameraYaw),
			Mathf.Sin(-clampedPitch),
			Mathf.Cos(clampedPitch) * Mathf.Cos(_cameraYaw));

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
		_boardCentroid = GetBoardCentroid();
		var focusLerp = Mathf.Clamp(delta * 2.5f, 0f, 1f);
		_boardCentroidSmoothed = _boardCentroidSmoothed.Lerp(_boardCentroid, _orbitDragging ? focusLerp * 0.5f : focusLerp);
		_cameraFocusOffset = _boardCentroidSmoothed + _cameraPanOffset;
		var smooth = (_orbitDragging || _isPanning) ? 0.18f : 0.14f;
		_cameraFocusSmoothedOffset = _cameraFocusSmoothedOffset.Lerp(_cameraFocusOffset, smooth);
	}

	private Vector3 GetBoardCentroid()
	{
		if (_session is null || _session.State.Board.Count == 0)
			return Vector3.Zero;

		var sum = Vector3.Zero;
		foreach (var slotIndex in _session.State.Board.Keys)
			sum += GetSlotPosition(slotIndex);

		return sum / _session.State.Board.Count;
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
		var coord = HexGrid.Decode(slotIndex);
		var x = _hexSpacingX * (coord.Q + coord.R * 0.5f);
		var z = _hexSpacingZ * coord.R;
		return new Vector3(x, 0, z);
	}

	private string ResolveSavePath()
	{
		var path = string.IsNullOrWhiteSpace(_savePathEdit.Text) ? DefaultSavePath : _savePathEdit.Text;
		return ProjectSettings.GlobalizePath(path);
	}

	private string ResolveHighScorePath() => ProjectSettings.GlobalizePath(DefaultHighScorePath);

	private void SetStatus(string message)
	{
		if (_statusLabel is not null)
			_statusLabel.Text = $"Status {CompactText(message, _minimalPlayMode ? 74 : 120)}";
		_eventSink.Publish(new UiNoteEvent(CompactText(message, 90)));
	}

	private static string CompactText(string text, int maxLen)
	{
		if (string.IsNullOrWhiteSpace(text))
			return "-";

		var compact = text.Replace('\r', ' ').Replace('\n', ' ').Trim();
		return compact.Length <= maxLen ? compact : (compact[..Math.Max(1, maxLen - 1)] + "…");
	}

	private static Color GetTileSwatchColor(TileType? tileType) => tileType switch
	{
		TileType.Plains => new Color(0.494f, 0.784f, 0.314f),
		TileType.Woods => new Color(0.227f, 0.420f, 0.251f),
		TileType.River => new Color(0.831f, 0.706f, 0.514f),
		TileType.Meadow => new Color(0.659f, 0.753f, 0.376f),
		TileType.Village => new Color(0.784f, 0.749f, 0.659f),
		TileType.Lake => new Color(0.549f, 0.482f, 0.431f),
		TileType.Desert => new Color(0.831f, 0.659f, 0.294f),
		TileType.Snow => new Color(0.867f, 0.910f, 0.925f),
		TileType.Volcano => new Color(0.361f, 0.227f, 0.165f),
		TileType.Pond => new Color(0.353f, 0.604f, 0.722f),
		TileType.Ruins => new Color(0.608f, 0.557f, 0.478f),
		TileType.Swamp => new Color(0.361f, 0.478f, 0.243f),
		TileType.Savanna => new Color(0.784f, 0.659f, 0.294f),
		TileType.Tundra => new Color(0.541f, 0.667f, 0.690f),
		TileType.Coral => new Color(0.831f, 0.459f, 0.416f),
		TileType.Mushroom => new Color(0.416f, 0.290f, 0.478f),
		_ => new Color(0.88f, 0.92f, 0.94f)
	};

	private static Color GetTileBevelColor(TileType tileType) => tileType switch
	{
		TileType.Plains => new Color(0.290f, 0.478f, 0.165f),
		TileType.Woods => new Color(0.176f, 0.353f, 0.208f),
		TileType.Lake => new Color(0.42f, 0.36f, 0.32f),
		TileType.River => new Color(0.769f, 0.584f, 0.416f),
		TileType.Village => new Color(0.541f, 0.494f, 0.431f),
		TileType.Meadow => new Color(0.478f, 0.541f, 0.227f),
		TileType.Desert => new Color(0.722f, 0.525f, 0.227f),
		TileType.Snow => new Color(0.627f, 0.722f, 0.753f),
		TileType.Pond => new Color(0.227f, 0.416f, 0.478f),
		TileType.Ruins => new Color(0.416f, 0.369f, 0.306f),
		TileType.Volcano => new Color(0.290f, 0.165f, 0.102f),
		TileType.Swamp => new Color(0.227f, 0.306f, 0.157f),
		TileType.Savanna => new Color(0.541f, 0.431f, 0.165f),
		TileType.Tundra => new Color(0.353f, 0.478f, 0.502f),
		TileType.Coral => new Color(0.722f, 0.333f, 0.290f),
		TileType.Mushroom => new Color(0.255f, 0.165f, 0.333f),
		_ => new Color(0.18f, 0.20f, 0.22f)
	};

	private static string GetTileDisplayName(TileType tileType) => tileType switch
	{
		TileType.Plains => "Plains",
		TileType.Woods => "Forest",
		TileType.Village => "City",
		TileType.Lake => "Mountain",
		TileType.Meadow => "Farmland",
		TileType.River => "Beach",
		TileType.Desert => "Desert",
		TileType.Snow => "Snow",
		TileType.Volcano => "Volcano",
		TileType.Pond => "Lake",
		TileType.Ruins => "Ruins",
		TileType.Swamp => "Swamp",
		TileType.Savanna => "Savanna",
		TileType.Tundra => "Tundra",
		TileType.Coral => "Coral Reef",
		TileType.Mushroom => "Mushroom Forest",
		_ => tileType.ToString()
	};

	private TileType? GetPlacedTileType(int slotIndex)
	{
		if (_session is null)
			return null;

		return _session.State.Board.TryGetValue(slotIndex, out var placed) ? placed.TileType : null;
	}

	private NeighborTypes GetNeighborTypes(int slotIndex)
	{
		var neighbors = HexGrid.GetNeighborSlots(slotIndex).ToArray();
		return new NeighborTypes(
			GetPlacedTileType(neighbors[2]),
			GetPlacedTileType(neighbors[0]),
			GetPlacedTileType(neighbors[5]),
			GetPlacedTileType(neighbors[3]));
	}

	private TileVisualSignature BuildVisualSignature(int slotIndex, TileType tileType, int rotationQuarterTurns)
	{
		return new TileVisualSignature(tileType, rotationQuarterTurns, null, null, null, null);
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

	private sealed class SlotVisual
	{
		public SlotVisual(int slotIndex, Node3D root, MeshInstance3D baseHex, StandardMaterial3D baseMaterial, Node3D overlay, Node3D tileRoot, float tileRootBaseY, float idlePhase, StandardMaterial3D overlayMaterial)
		{
			SlotIndex = slotIndex;
			Root = root;
			BaseHex = baseHex;
			BaseMaterial = baseMaterial;
			Overlay = overlay;
			TileRoot = tileRoot;
			TileRootBaseY = tileRootBaseY;
			IdlePhase = idlePhase;
			OverlayMaterial = overlayMaterial;
		}

		public int SlotIndex { get; }
		public Node3D Root { get; }
		public MeshInstance3D BaseHex { get; }
		public StandardMaterial3D BaseMaterial { get; }
		public Node3D Overlay { get; }
		public Node3D TileRoot { get; }
		public float TileRootBaseY { get; }
		public float IdlePhase { get; }
		public StandardMaterial3D OverlayMaterial { get; }
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

	private sealed class FloatingPopup
	{
		public FloatingPopup(Label3D label, Vector3 startPosition, Vector3 endPosition, float duration)
		{
			Label = label;
			StartPosition = startPosition;
			EndPosition = endPosition;
			Duration = duration;
			Elapsed = 0f;
		}

		public Label3D Label { get; }
		public Vector3 StartPosition { get; }
		public Vector3 EndPosition { get; }
		public float Duration { get; }
		public float Elapsed { get; set; }
	}

	private readonly record struct TileComposition(
		float WaterDominance,
		float PatchSpread,
		float PatchDensity,
		float PatchDensityScale,
		float DetailScale,
		int DetailBias,
		float Dryness,
		float HeightScale);

	private readonly record struct SectorRegion(TileType Terrain, int[] Sectors);

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
