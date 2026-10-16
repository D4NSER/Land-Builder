using Godot;

namespace LandBuilder.Presentation;

/// <summary>
/// Orbit camera with inertia. Zoom, orbit and pan are all eased; the rig gently re-frames the island as it grows
/// unless the player has taken control. Also handles screen shake and a depth-of-field "tilt-shift" look.
/// </summary>
public partial class CameraRig : Node3D
{
    public const float MinDistance = 4.5f;
    public const float MaxDistance = 34f;
    private const float MinPitch = 0.42f;
    private const float MaxPitch = 1.3f;

    private Camera3D _camera = null!;
    private CameraAttributesPractical _attributes = null!;
    private Vector3 _focus;
    private Vector3 _focusTarget;
    private float _yaw = 0.6f;
    private float _yawTarget = 0.6f;
    private float _pitch = 0.9f;
    private float _pitchTarget = 0.9f;
    private float _distance = 9f;
    private float _distanceTarget = 9f;
    private float _trauma;
    private float _time;
    private float _userControlTimer;
    private float _autoOrbitSpeed;

    public Camera3D Camera => _camera;

    /// <summary>Slides the framing sideways (negative = island appears to the right), e.g. to make room for menus.</summary>
    public float ScreenShift { get; set; }
    public bool DepthOfField { get; set; } = true;

    /// <summary>Radius of the island the auto-framing tries to keep in view.</summary>
    public float FrameRadius { get; set; } = 2f;

    /// <summary>Where the camera drifts back to (the island centre) when the player isn't steering.</summary>
    public Vector3 AutoFocus { get; set; }

    public Vector3 FocusTarget
    {
        get => _focusTarget;
        set => _focusTarget = value;
    }

    public override void _Ready()
    {
        _attributes = new CameraAttributesPractical
        {
            DofBlurFarEnabled = true,
            DofBlurFarDistance = 18f,
            DofBlurFarTransition = 14f,
            DofBlurNearEnabled = true,
            DofBlurNearDistance = 3f,
            DofBlurNearTransition = 2f,
            DofBlurAmount = 0.06f
        };
        _camera = new Camera3D { Fov = 38f, Near = 0.05f, Far = 400f, Attributes = _attributes, Current = true };
        AddChild(_camera);
        Apply(0f);
    }

    public void Orbit(Vector2 screenDelta)
    {
        _yawTarget -= screenDelta.X * 0.006f;
        _pitchTarget = Mathf.Clamp(_pitchTarget + (screenDelta.Y * 0.005f), MinPitch, MaxPitch);
        _userControlTimer = 6f;
    }

    public void RotateBy(float radians)
    {
        _yawTarget += radians;
        _userControlTimer = 6f;
    }

    public void Zoom(float steps)
    {
        _distanceTarget = Mathf.Clamp(_distanceTarget * Mathf.Pow(1.12f, steps), MinDistance, MaxDistance);
        _userControlTimer = 8f;
    }

    public void Pan(Vector2 screenDelta)
    {
        var right = new Vector3(Mathf.Cos(_yaw), 0, -Mathf.Sin(_yaw));
        var forward = new Vector3(Mathf.Sin(_yaw), 0, Mathf.Cos(_yaw));
        var scale = _distance * 0.0016f;
        _focusTarget += (-right * screenDelta.X * scale) + (-forward * screenDelta.Y * scale);
        _userControlTimer = 8f;
    }

    public void PanWorld(Vector2 input, float delta)
    {
        if (input == Vector2.Zero)
            return;
        var right = new Vector3(Mathf.Cos(_yaw), 0, -Mathf.Sin(_yaw));
        var forward = new Vector3(Mathf.Sin(_yaw), 0, Mathf.Cos(_yaw));
        _focusTarget += ((right * input.X) + (forward * input.Y)) * _distance * 0.9f * delta;
        _userControlTimer = 8f;
    }

    public void Shake(float amount) => _trauma = Mathf.Min(1f, _trauma + amount);

    public void SetAutoOrbit(float speed) => _autoOrbitSpeed = speed;

    /// <summary>Recentres and re-frames the island (F key / after a reset).</summary>
    public void Reframe(Vector3 centre, float radius, bool instant = false)
    {
        _focusTarget = centre;
        FrameRadius = radius;
        _distanceTarget = DistanceFor(radius);
        _pitchTarget = 0.92f;
        _userControlTimer = 0f;
        if (instant)
        {
            _focus = _focusTarget;
            _distance = _distanceTarget;
            _pitch = _pitchTarget;
            _yaw = _yawTarget;
            Apply(0f);
        }
    }

    /// <summary>Starts high and far away, then swoops down onto the island.</summary>
    public void IntroSwoop()
    {
        _distance = MaxDistance * 1.3f;
        _pitch = MaxPitch;
        _yaw = _yawTarget - 1.2f;
    }

    private float DistanceFor(float radius) => Mathf.Clamp(6.5f + (radius * 1.9f), MinDistance + 2f, MaxDistance * 0.85f);

    public override void _Process(double delta)
    {
        var dt = (float)delta;
        _time += dt;
        _userControlTimer = Mathf.Max(0f, _userControlTimer - dt);

        if (_userControlTimer <= 0f)
            _focusTarget = _focusTarget.Lerp(AutoFocus, Ease.Damp(1.2f, dt));
        if (_userControlTimer <= 0f)
            _distanceTarget = Mathf.Lerp(_distanceTarget, Mathf.Max(_distanceTarget, DistanceFor(FrameRadius)), Ease.Damp(1.5f, dt));

        _yawTarget += _autoOrbitSpeed * dt;

        _focus = _focus.Lerp(_focusTarget, Ease.Damp(4f, dt));
        _yaw = Mathf.Lerp(_yaw, _yawTarget, Ease.Damp(9f, dt));
        _pitch = Mathf.Lerp(_pitch, _pitchTarget, Ease.Damp(7f, dt));
        _distance = Mathf.Lerp(_distance, _distanceTarget, Ease.Damp(5f, dt));
        _trauma = Mathf.Max(0f, _trauma - (dt * 1.8f));
        Apply(dt);
    }

    private void Apply(float dt)
    {
        var offset = new Vector3(
            Mathf.Sin(_yaw) * Mathf.Cos(_pitch),
            Mathf.Sin(_pitch),
            Mathf.Cos(_yaw) * Mathf.Cos(_pitch)) * _distance;
        var position = _focus + offset;
        var shake = _trauma * _trauma;
        if (shake > 0f)
        {
            position += new Vector3(
                (Noise.Value(_time * 25f, 0f, 1) - 0.5f) * shake * 0.25f,
                (Noise.Value(_time * 25f, 10f, 2) - 0.5f) * shake * 0.25f,
                (Noise.Value(_time * 25f, 20f, 3) - 0.5f) * shake * 0.25f);
        }

        if (_camera is null)
            return;
        _camera.GlobalPosition = position;
        _camera.HOffset = Mathf.Lerp(_camera.HOffset, ScreenShift * _distance * 0.2f, dt <= 0f ? 1f : Ease.Damp(3f, dt));
        _camera.LookAt(_focus + new Vector3(0, 0.1f, 0), Vector3.Up);

        if (_attributes is not null)
        {
            _attributes.DofBlurFarEnabled = DepthOfField;
            _attributes.DofBlurNearEnabled = DepthOfField;
            _attributes.DofBlurFarDistance = _distance + (FrameRadius * 0.9f) + 2.5f;
            _attributes.DofBlurFarTransition = _distance * 1.2f;
            _attributes.DofBlurNearDistance = Mathf.Max(1.5f, _distance * 0.45f);
            _attributes.DofBlurNearTransition = _distance * 0.3f;
        }
    }

    public float Yaw => _yaw;
}
