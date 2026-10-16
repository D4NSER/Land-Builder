using System.Collections.Generic;
using Godot;

namespace LandBuilder.Presentation;

public enum FxKind
{
    Dust,
    Leaves,
    Sparkle,
    Confetti,
    Splash
}

/// <summary>One-shot particle bursts with real gravity and drag: landing dust, leaves, sparkles, confetti, splashes.</summary>
public partial class Fx : Node3D
{
    private readonly Dictionary<FxKind, (ParticleProcessMaterial Process, Mesh Mesh, int Amount, float Lifetime)> _kinds = new();
    private float _intensity = 1f;

    public float Intensity
    {
        get => _intensity;
        set => _intensity = Mathf.Clamp(value, 0f, 1.5f);
    }

    public override void _Ready()
    {
        _kinds[FxKind.Dust] = (Process(
            gravity: new Vector3(0, -0.6f, 0), velocity: (0.4f, 1.1f), spread: 80f, direction: new Vector3(0, 0.35f, 0),
            scale: (0.6f, 1.4f), colors: [new Color("e9dcc0"), new Color("cdb995"), new Color("f4ecdc", 0)], damping: 2.4f,
            emissionRing: true), PuffMesh(0.06f), 28, 0.9f);

        _kinds[FxKind.Leaves] = (Process(
            gravity: new Vector3(0, -1.2f, 0), velocity: (0.8f, 1.8f), spread: 55f, direction: Vector3.Up,
            scale: (0.6f, 1.2f), colors: [new Color("7cc456"), new Color("4f9a4c"), new Color("a9d65e", 0)], damping: 1.4f,
            spin: true), LeafMesh(), 22, 1.4f);

        _kinds[FxKind.Sparkle] = (Process(
            gravity: new Vector3(0, -0.4f, 0), velocity: (1.2f, 2.6f), spread: 70f, direction: Vector3.Up,
            scale: (0.5f, 1.2f), colors: [new Color("fff6c8"), new Color("ffd25e"), new Color("ffb13b", 0)], damping: 2.0f,
            spin: true), StarMesh(), 36, 1.2f);

        _kinds[FxKind.Confetti] = (Process(
            gravity: new Vector3(0, -2.2f, 0), velocity: (2.0f, 3.6f), spread: 40f, direction: Vector3.Up,
            scale: (0.7f, 1.3f), colors: [new Color("e8665a"), new Color("ffd25e"), new Color("6cc070"), new Color("5ab0e8"), new Color("c78be8", 0)], damping: 1.2f,
            spin: true, randomColor: true), ConfettiMesh(), 70, 2.2f);

        _kinds[FxKind.Splash] = (Process(
            gravity: new Vector3(0, -3.5f, 0), velocity: (0.8f, 1.6f), spread: 35f, direction: Vector3.Up,
            scale: (0.5f, 1.1f), colors: [new Color("ffffff"), new Color("bfe9f5"), new Color("8fd8d0", 0)], damping: 0.6f,
            emissionRing: true), PuffMesh(0.035f), 26, 0.8f);
    }

    public void Burst(FxKind kind, Vector3 position, float scale = 1f)
    {
        if (_intensity <= 0.01f)
            return;

        var (process, mesh, amount, lifetime) = _kinds[kind];
        var particles = new GpuParticles3D
        {
            Amount = Mathf.Max(1, Mathf.RoundToInt(amount * _intensity * scale)),
            Lifetime = lifetime,
            OneShot = true,
            Explosiveness = 0.92f,
            Randomness = 0.4f,
            ProcessMaterial = process,
            DrawPass1 = mesh,
            Position = position,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            VisibilityAabb = new Aabb(new Vector3(-4, -2, -4), new Vector3(8, 8, 8)),
            Scale = Vector3.One * scale
        };
        AddChild(particles);
        particles.Emitting = true;
        particles.Finished += particles.QueueFree;
    }

    private static ParticleProcessMaterial Process(
        Vector3 gravity, (float Min, float Max) velocity, float spread, Vector3 direction, (float Min, float Max) scale,
        Color[] colors, float damping, bool emissionRing = false, bool spin = false, bool randomColor = false)
    {
        var mat = new ParticleProcessMaterial
        {
            Gravity = gravity,
            InitialVelocityMin = velocity.Min,
            InitialVelocityMax = velocity.Max,
            Spread = spread,
            Direction = direction == Vector3.Zero ? Vector3.Up : direction.Normalized(),
            ScaleMin = scale.Min,
            ScaleMax = scale.Max,
            DampingMin = damping * 0.7f,
            DampingMax = damping
        };

        if (emissionRing)
        {
            mat.EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Ring;
            mat.EmissionRingAxis = Vector3.Up;
            mat.EmissionRingRadius = 0.85f;
            mat.EmissionRingInnerRadius = 0.6f;
            mat.EmissionRingHeight = 0.05f;
        }
        else
        {
            mat.EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Sphere;
            mat.EmissionSphereRadius = 0.25f;
        }

        if (spin)
        {
            mat.AngularVelocityMin = -360f;
            mat.AngularVelocityMax = 360f;
            mat.AngleMin = 0f;
            mat.AngleMax = 360f;
        }

        var offsets = new float[colors.Length];
        for (var i = 0; i < colors.Length; i++)
            offsets[i] = colors.Length == 1 ? 0 : i / (float)(colors.Length - 1);
        var gradient = new Gradient { Offsets = offsets, Colors = colors };

        if (randomColor)
        {
            mat.ColorInitialRamp = new GradientTexture1D { Gradient = gradient };
            var fade = new Gradient { Offsets = [0.7f, 1f], Colors = [Colors.White, new Color(1, 1, 1, 0)] };
            mat.ColorRamp = new GradientTexture1D { Gradient = fade };
        }
        else
        {
            mat.ColorRamp = new GradientTexture1D { Gradient = gradient };
        }

        var curve = new Curve();
        curve.AddPoint(new Vector2(0, 0.2f));
        curve.AddPoint(new Vector2(0.15f, 1f));
        curve.AddPoint(new Vector2(1, 0.1f));
        mat.ScaleCurve = new CurveTexture { Curve = curve };
        return mat;
    }

    private static StandardMaterial3D ParticleMaterial(bool billboard) => new()
    {
        VertexColorUseAsAlbedo = true,
        Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
        ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
        BillboardMode = billboard ? BaseMaterial3D.BillboardModeEnum.Particles : BaseMaterial3D.BillboardModeEnum.Disabled,
        CullMode = BaseMaterial3D.CullModeEnum.Disabled
    };

    private static Mesh PuffMesh(float size)
    {
        var mesh = new SphereMesh { Radius = size, Height = size * 2f, RadialSegments = 6, Rings = 3 };
        mesh.Material = ParticleMaterial(false);
        return mesh;
    }

    private static Mesh LeafMesh()
    {
        var mesh = new QuadMesh { Size = new Vector2(0.05f, 0.03f) };
        mesh.Material = ParticleMaterial(true);
        return mesh;
    }

    private static Mesh StarMesh()
    {
        var mesh = new QuadMesh { Size = new Vector2(0.06f, 0.06f) };
        var mat = ParticleMaterial(true);
        mat.BlendMode = BaseMaterial3D.BlendModeEnum.Add;
        mat.AlbedoTexture = SoftDot();
        mesh.Material = mat;
        return mesh;
    }

    private static Mesh ConfettiMesh()
    {
        var mesh = new QuadMesh { Size = new Vector2(0.05f, 0.025f) };
        mesh.Material = ParticleMaterial(true);
        return mesh;
    }

    private static Texture2D SoftDot()
    {
        var gradient = new Gradient();
        gradient.SetColor(0, Colors.White);
        gradient.SetColor(1, new Color(1, 1, 1, 0));
        return new GradientTexture2D
        {
            Gradient = gradient,
            Fill = GradientTexture2D.FillEnum.Radial,
            FillFrom = new Vector2(0.5f, 0.5f),
            FillTo = new Vector2(0.5f, 0f),
            Width = 32,
            Height = 32
        };
    }
}
