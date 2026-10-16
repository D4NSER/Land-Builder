using System.Collections.Generic;
using Godot;

namespace LandBuilder.Presentation;

/// <summary>Sky, sun, sea, seabed and drifting clouds. Quality settings toggle the expensive bits.</summary>
public partial class WorldView : Node3D
{
    public const float OceanLevel = -0.3f;

    private readonly List<(Node3D Node, Vector3 Velocity, float Bob)> _clouds = new();
    private readonly List<(Node3D Node, float Radius, float Speed, float Phase, float Height)> _birds = new();
    private Godot.Environment _environment = null!;
    private DirectionalLight3D _sun = null!;
    private MeshInstance3D _ocean = null!;
    private float _time;
    private ProceduralSkyMaterial _sky = null!;
    private float _daylight = 0.3f;
    private float _daylightTarget = 0.3f;

    /// <summary>0 = fresh morning, 0.4 = midday, 1 = golden hour. Eased towards over a few seconds.</summary>
    public float DaylightTarget
    {
        get => _daylightTarget;
        set => _daylightTarget = Mathf.Clamp(value, 0f, 1f);
    }

    public Godot.Environment Environment => _environment;
    public DirectionalLight3D Sun => _sun;

    public override void _Ready()
    {
        var sky = new ProceduralSkyMaterial
        {
            SkyTopColor = new Color("5aa6dc"),
            SkyHorizonColor = new Color("cfe8f2"),
            SkyCurve = 0.12f,
            GroundBottomColor = new Color("2a7fa6"),
            GroundHorizonColor = new Color("cfe8f2"),
            SunAngleMax = 20f,
            SunCurve = 0.08f
        };

        _sky = sky;
        _environment = new Godot.Environment
        {
            BackgroundMode = Godot.Environment.BGMode.Sky,
            Sky = new Sky { SkyMaterial = sky },
            AmbientLightSource = Godot.Environment.AmbientSource.Sky,
            AmbientLightSkyContribution = 0.85f,
            AmbientLightEnergy = 0.8f,
            ReflectedLightSource = Godot.Environment.ReflectionSource.Sky,
            TonemapMode = Godot.Environment.ToneMapper.Filmic,
            TonemapExposure = 1.05f,
            TonemapWhite = 6f,
            GlowEnabled = true,
            GlowIntensity = 0.4f,
            GlowStrength = 0.9f,
            GlowBloom = 0.04f,
            GlowHdrThreshold = 1.0f,
            GlowBlendMode = Godot.Environment.GlowBlendModeEnum.Softlight,
            SsaoEnabled = true,
            SsaoRadius = 0.7f,
            SsaoIntensity = 1.6f,
            SsaoPower = 1.3f,
            SsaoDetail = 0.6f,
            FogEnabled = true,
            FogLightColor = new Color("cfe8f2"),
            FogDensity = 0.006f,
            FogSkyAffect = 0.0f,
            FogAerialPerspective = 0.25f,
            AdjustmentEnabled = true,
            AdjustmentSaturation = 1.05f,
            AdjustmentContrast = 1.04f,
            AdjustmentBrightness = 1.0f
        };

        AddChild(new WorldEnvironment { Environment = _environment });

        _sun = new DirectionalLight3D
        {
            Name = "Sun",
            LightColor = new Color("fff1dc"),
            LightEnergy = 1.55f,
            ShadowEnabled = true,
            ShadowBlur = 1.6f,
            ShadowBias = 0.04f,
            ShadowNormalBias = 1.2f,
            DirectionalShadowMode = DirectionalLight3D.ShadowMode.Parallel4Splits,
            DirectionalShadowMaxDistance = 45f,
            DirectionalShadowBlendSplits = true,
            LightAngularDistance = 1.5f,
            Rotation = new Vector3(Mathf.DegToRad(-52f), Mathf.DegToRad(38f), 0)
        };
        AddChild(_sun);

        // Soft cool rim from the opposite side keeps shadows from going muddy.
        AddChild(new DirectionalLight3D
        {
            Name = "Rim",
            LightColor = new Color("bcd7ff"),
            LightEnergy = 0.25f,
            ShadowEnabled = false,
            Rotation = new Vector3(Mathf.DegToRad(-30f), Mathf.DegToRad(-140f), 0),
            LightSpecular = 0.1f
        });

        BuildOcean();
        BuildClouds();
        BuildBirds();
    }

    private void BuildOcean()
    {
        var water = new ShaderMaterial { Shader = GD.Load<Shader>("res://shaders/water.gdshader") };
        water.SetShaderParameter("shallow_color", new Color("4fc6c6"));
        water.SetShaderParameter("deep_color", new Color("1e6aa3"));
        water.SetShaderParameter("depth_scale", 2.0f);
        water.SetShaderParameter("foam_width", 0.14f);
        water.SetShaderParameter("min_alpha", 0.62f);
        water.SetShaderParameter("wave_height", 0.018f);
        water.SetShaderParameter("ripple_scale", 1.4f);
        water.SetShaderParameter("ripple_strength", 0.5f);
        water.SetShaderParameter("far_fade", 70f);

        _ocean = new MeshInstance3D
        {
            Name = "Ocean",
            Mesh = new PlaneMesh { Size = new Vector2(260, 260), SubdivideWidth = 160, SubdivideDepth = 160 },
            MaterialOverride = water,
            Position = new Vector3(0, OceanLevel, 0),
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off
        };
        AddChild(_ocean);

        AddChild(new MeshInstance3D
        {
            Name = "Seabed",
            Mesh = BuildSeabed(),
            MaterialOverride = new StandardMaterial3D { VertexColorUseAsAlbedo = true, Roughness = 1f },
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off
        });
    }

    /// <summary>
    /// Radial seabed in unit space: a sandy shelf right under the island that slopes away into the deep.
    /// The node is scaled with the island so the turquoise shallows always hug the coast.
    /// </summary>
    private static ArrayMesh BuildSeabed()
    {
        var kit = new MeshKit();
        const int segments = 72;
        float[] radii = [0f, 0.6f, 0.9f, 1.1f, 1.35f, 1.7f, 2.2f, 3.2f, 5f, 9f];
        // Gentle bowl: a little shallower under the island, deep blue everywhere else.
        float Height(float r) => Mathf.Lerp(-2.6f, -5f, Mathf.SmoothStep(0.6f, 2.5f, r));
        Color ColorAt(float r)
        {
            var teal = new Color("3a8ea6");
            var deep = new Color("1d4f7a");
            return teal.Lerp(deep, Mathf.SmoothStep(0.4f, 2.0f, r));
        }

        for (var ring = 0; ring < radii.Length - 1; ring++)
        {
            var r0 = radii[ring];
            var r1 = radii[ring + 1];
            for (var i = 0; i < segments; i++)
            {
                var a0 = i * Mathf.Tau / segments;
                var a1 = (i + 1) * Mathf.Tau / segments;
                Vector3 P(float r, float a) => new(Mathf.Cos(a) * r, Height(r), -Mathf.Sin(a) * r);
                var p00 = P(r0, a0);
                var p01 = P(r0, a1);
                var p10 = P(r1, a0);
                var p11 = P(r1, a1);
                var up = Vector3.Up;
                var c0 = ColorAt(r0);
                var c1 = ColorAt(r1);
                // Counter-clockwise from above.
                kit.TriangleSmooth(p00, p10, p11, up, up, up, c0, c1, c1);
                if (r0 > 0f)
                    kit.TriangleSmooth(p00, p11, p01, up, up, up, c0, c1, c0);
            }
        }

        return kit.Commit(withCustom: false);
    }

    private float _shelfRadius = 6f;
    private Vector3 _shelfCentre;

    public void SetShelf(Vector3 centre, float radius)
    {
        _shelfRadius = Mathf.Max(5f, radius + 2.5f);
        _shelfCentre = new Vector3(centre.X, 0, centre.Z);
    }

    private void BuildClouds()
    {
        var mat = new StandardMaterial3D
        {
            AlbedoColor = new Color(1f, 1f, 1f),
            Roughness = 1f,
            RimEnabled = true,
            Rim = 0.4f,
            RimTint = 0.3f
        };
        var rng = new RandomNumberGenerator { Seed = 4242 };
        for (var i = 0; i < 9; i++)
        {
            var kit = new MeshKit();
            var puffs = rng.RandiRange(4, 7);
            for (var p = 0; p < puffs; p++)
            {
                var offset = new Vector3(rng.RandfRange(-1.4f, 1.4f), rng.RandfRange(0f, 0.4f), rng.RandfRange(-0.6f, 0.6f));
                var r = rng.RandfRange(0.5f, 0.95f);
                kit.Transform = new Transform3D(Basis.Identity, offset);
                kit.Blob(new Vector3(r, r * 0.7f, r * 0.85f), Colors.White, rng.RandiRange(0, 999), 0.1f);
            }

            var cloud = new MeshInstance3D
            {
                Mesh = kit.Commit(withCustom: false),
                MaterialOverride = mat,
                CastShadow = GeometryInstance3D.ShadowCastingSetting.On
            };
            // Clouds live out near the horizon so they frame the island without ever blocking the view.
            var angle = (i / 9f * Mathf.Tau) + rng.RandfRange(-0.3f, 0.3f);
            var dist = rng.RandfRange(48f, 75f);
            var node = new Node3D { Position = new Vector3(Mathf.Cos(angle) * dist, rng.RandfRange(4f, 9f), Mathf.Sin(angle) * dist) };
            node.Scale = Vector3.One * rng.RandfRange(2.5f, 4.5f);
            node.AddChild(cloud);
            AddChild(node);
            _clouds.Add((node, new Vector3(rng.RandfRange(0.12f, 0.25f), 0, rng.RandfRange(-0.05f, 0.05f)), rng.Randf() * 10f));
        }
    }

    private void BuildBirds()
    {
        var mat = new StandardMaterial3D { AlbedoColor = new Color("f4f4f0"), Roughness = 1f, CullMode = BaseMaterial3D.CullModeEnum.Disabled };
        var rng = new RandomNumberGenerator { Seed = 99 };
        for (var i = 0; i < 4; i++)
        {
            var bird = new Node3D();
            for (var side = -1; side <= 1; side += 2)
            {
                var wing = new MeshInstance3D
                {
                    Mesh = new PrismMesh { Size = new Vector3(0.12f, 0.01f, 0.05f) },
                    MaterialOverride = mat,
                    Position = new Vector3(0.06f * side, 0, 0),
                    CastShadow = GeometryInstance3D.ShadowCastingSetting.On,
                    Name = side < 0 ? "L" : "R"
                };
                bird.AddChild(wing);
            }

            AddChild(bird);
            _birds.Add((bird, rng.RandfRange(4f, 9f), rng.RandfRange(0.15f, 0.3f) * (rng.Randf() < 0.5f ? -1 : 1), rng.Randf() * Mathf.Tau, rng.RandfRange(2.2f, 3.4f)));
        }
    }

    public Vector3 BirdCenter { get; set; }

    public override void _Process(double delta)
    {
        var dt = (float)delta;
        _time += dt;
        _daylight = Mathf.Lerp(_daylight, _daylightTarget, Ease.Damp(0.6f, dt));
        ApplyDaylight(_daylight);

        if (GetNodeOrNull<Node3D>("Seabed") is { } seabed)
        {
            var scale = Mathf.Lerp(seabed.Scale.X, _shelfRadius, Ease.Damp(1.5f, dt));
            seabed.Scale = new Vector3(scale, 1f, scale);
            seabed.Position = seabed.Position.Lerp(_shelfCentre, Ease.Damp(1.5f, dt));
        }

        for (var i = 0; i < _clouds.Count; i++)
        {
            var (node, velocity, bob) = _clouds[i];
            var p = node.Position + (velocity * dt);
            if (p.X > 80f)
                p.X = -80f;
            p.Y += Mathf.Sin((_time * 0.2f) + bob) * 0.0015f;
            node.Position = p;
        }

        foreach (var (node, radius, speed, phase, height) in _birds)
        {
            var a = phase + (_time * speed);
            var pos = BirdCenter + new Vector3(Mathf.Cos(a) * radius, height + (Mathf.Sin((_time * 0.7f) + phase) * 0.2f), Mathf.Sin(a) * radius);
            node.Position = pos;
            var tangent = new Vector3(-Mathf.Sin(a), 0, Mathf.Cos(a)) * Mathf.Sign(speed);
            node.LookAt(pos + tangent, Vector3.Up);
            var flap = Mathf.Sin((_time * 9f) + phase) * 0.6f;
            node.GetNode<Node3D>("L").Rotation = new Vector3(0, 0, -flap);
            node.GetNode<Node3D>("R").Rotation = new Vector3(0, 0, flap);
        }
    }

    private void ApplyDaylight(float t)
    {
        // Keyframes: morning, midday, golden hour.
        static float Key3(float t, float a, float b, float c) => t < 0.4f ? Mathf.Lerp(a, b, t / 0.4f) : Mathf.Lerp(b, c, (t - 0.4f) / 0.6f);
        static Color Col3(float t, Color a, Color b, Color c) => t < 0.4f ? a.Lerp(b, t / 0.4f) : b.Lerp(c, (t - 0.4f) / 0.6f);

        _sun.Rotation = new Vector3(
            Mathf.DegToRad(Key3(t, -34f, -56f, -22f)),
            Mathf.DegToRad(Key3(t, 70f, 38f, -12f)),
            0f);
        _sun.LightColor = Col3(t, new Color("fff4e2"), new Color("fff1dc"), new Color("ffd49c"));
        _sun.LightEnergy = Key3(t, 1.4f, 1.55f, 1.45f);
        _sky.SkyTopColor = Col3(t, new Color("63a9dc"), new Color("5aa6dc"), new Color("5b9ad3"));
        _sky.SkyHorizonColor = Col3(t, new Color("d6ecf2"), new Color("cfe8f2"), new Color("f2e0c6"));
        _sky.GroundHorizonColor = _sky.SkyHorizonColor;
        _environment.FogLightColor = _sky.SkyHorizonColor;
        _environment.AmbientLightEnergy = Key3(t, 0.85f, 0.8f, 0.78f);
    }

    public void ApplyQuality(bool shadows, bool ssao, bool glow)
    {
        _sun.ShadowEnabled = shadows;
        _environment.SsaoEnabled = ssao;
        _environment.GlowEnabled = glow;
    }
}
