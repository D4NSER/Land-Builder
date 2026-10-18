using System;
using Godot;
using LandBuilder.Domain;

namespace LandBuilder.Presentation;

/// <summary>A little 3D diorama of a tile rendered into the HUD (the stack card and the "next" slot).</summary>
public partial class TilePreview : SubViewportContainer
{
    private readonly Func<TileSpec, int, TileVisual> _factory;
    private readonly float _cameraDistance;
    private SubViewport _viewport = null!;
    private Node3D _pivot = null!;
    private TileVisual? _visual;
    private TileSpec? _tile;
    private Spring _scale = new(1f, 260f, 14f);
    private float _spin;
    private float _targetYaw;
    private float _yaw;

    public bool Spin { get; set; } = true;

    public TilePreview(Func<TileSpec, int, TileVisual> factory, int pixelSize, float cameraDistance = 4.6f)
    {
        _factory = factory;
        _cameraDistance = cameraDistance;
        Stretch = true;
        CustomMinimumSize = new Vector2(pixelSize, pixelSize);
        MouseFilter = MouseFilterEnum.Ignore;
    }

    public override void _Ready()
    {
        _viewport = new SubViewport
        {
            OwnWorld3D = true,
            TransparentBg = true,
            Msaa3D = Viewport.Msaa.Msaa4X,
            RenderTargetUpdateMode = SubViewport.UpdateMode.Always
        };
        AddChild(_viewport);

        var env = new Godot.Environment
        {
            BackgroundMode = Godot.Environment.BGMode.ClearColor,
            AmbientLightSource = Godot.Environment.AmbientSource.Color,
            AmbientLightColor = new Color("dfeaf0"),
            AmbientLightEnergy = 0.75f,
            TonemapMode = Godot.Environment.ToneMapper.Filmic,
            AdjustmentEnabled = true,
            AdjustmentSaturation = 1.12f
        };
        _viewport.AddChild(new WorldEnvironment { Environment = env });
        _viewport.AddChild(new DirectionalLight3D
        {
            LightColor = new Color("fff1dc"),
            LightEnergy = 1.3f,
            ShadowEnabled = true,
            Rotation = new Vector3(Mathf.DegToRad(-55f), Mathf.DegToRad(30f), 0)
        });

        _pivot = new Node3D();
        _viewport.AddChild(_pivot);
        var camera = new Camera3D { Fov = 32f, Current = true };
        _viewport.AddChild(camera);
        camera.Position = new Vector3(0, _cameraDistance * 0.8f, _cameraDistance * 0.6f);
        camera.LookAt(new Vector3(0, -0.12f, 0), Vector3.Up);
    }

    public void SetTile(TileSpec? tile, int rotation = 0, bool animate = true)
    {
        if (tile == _tile && _visual is not null)
        {
            SetRotation(rotation);
            return;
        }

        _tile = tile;
        _visual?.Free();
        _visual = null;
        if (tile is null)
            return;

        // Building a new preview is a full tile build; wait for a frame with time to spare.
        _pendingRotation = rotation;
        _pendingAnimate = animate;
        _pending = true;
        if (FrameBudget.TryClaim())
            BuildPending();
    }

    private bool _pending;
    private int _pendingRotation;
    private bool _pendingAnimate;

    private void BuildPending()
    {
        _pending = false;
        if (_tile is not TileSpec tile)
            return;
        var rotation = _pendingRotation;
        var animate = _pendingAnimate;
        _visual = _factory(tile, rotation);
        _pivot.AddChild(_visual.Root);
        _visual.SetGrow(1f);
        _targetYaw = HexLayout.RotationToYaw(rotation);
        _yaw = _targetYaw;
        if (animate)
        {
            _scale.Snap(0.2f);
            _scale.Target = 1f;
        }
    }

    public void SetRotation(int rotation)
    {
        var target = HexLayout.RotationToYaw(rotation);
        // Take the short way round.
        var diff = Mathf.Wrap(target - _targetYaw, -Mathf.Pi, Mathf.Pi);
        _targetYaw += diff;
    }

    public void Bump()
    {
        _scale.Velocity += 4f;
    }

    public override void _Process(double delta)
    {
        if (_pending && FrameBudget.TryClaim())
            BuildPending();
        var dt = (float)delta;
        _scale.Step(dt);
        _spin += dt;
        _yaw = Mathf.Lerp(_yaw, _targetYaw, Ease.Damp(14f, dt));
        if (_visual is not null)
        {
            _visual.Body.Rotation = new Vector3(0, _yaw, 0);
            _visual.Root.Position = new Vector3(0, Spin ? Mathf.Sin(_spin * 2f) * 0.03f : 0f, 0);
            _visual.Root.Scale = Vector3.One * Mathf.Max(0.01f, _scale.Value);
        }

        _pivot.Rotation = new Vector3(0, Spin ? Mathf.Sin(_spin * 0.5f) * 0.25f : 0f, 0);
    }
}
