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
    private ShaderMaterial _sky = null!;
    private ShaderMaterial _oceanMaterial = null!;
    private GpuParticles3D _motes = null!;
    private ThemeStyle _style = ThemeStyle.For(LandBuilder.Domain.WorldTheme.Temperate);
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
        var sky = new ShaderMaterial { Shader = GD.Load<Shader>("res://shaders/sky.gdshader") };
        _sky = sky;
        _environment = new Godot.Environment
        {
            BackgroundMode = Godot.Environment.BGMode.Sky,
            Sky = new Sky { SkyMaterial = sky, ProcessMode = Sky.ProcessModeEnum.Incremental, RadianceSize = Sky.RadianceSizeEnum.Size128 },
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
            SsaoRadius = 0.8f,
            SsaoIntensity = 2.1f,
            SsaoPower = 1.3f,
            SsaoDetail = 0.6f,
            FogEnabled = true,
            FogLightColor = new Color("cfe8f2"),
            // Depth fog that only starts beyond the island: distant sea melts into the sky, the island stays crisp.
            FogMode = Godot.Environment.FogModeEnum.Depth,
            FogDensity = 1.0f,
            FogDepthBegin = 30f,
            FogDepthEnd = 140f,
            FogDepthCurve = 1.3f,
            FogSkyAffect = 0.0f,
            FogAerialPerspective = 0.0f,
            AdjustmentEnabled = true,
            AdjustmentSaturation = 1.05f,
            AdjustmentContrast = 1.06f,
            AdjustmentBrightness = 1.0f,
            AdjustmentColorCorrection = GradingLut()
        };

        AddChild(new WorldEnvironment { Environment = _environment });

        _sun = new DirectionalLight3D
        {
            Name = "Sun",
            LightColor = new Color("fff1dc"),
            LightEnergy = 1.55f,
            ShadowEnabled = true,
            ShadowBlur = 1.1f,
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
        BuildMotes();
    }

    /// <summary>Gentle split-tone grade: slightly cool shadows, warm highlights.</summary>
    private static GradientTexture1D GradingLut()
    {
        var gradient = new Gradient
        {
            Offsets = [0f, 0.25f, 0.55f, 1f],
            Colors = [new Color(0.0f, 0.01f, 0.04f), new Color(0.23f, 0.25f, 0.29f), new Color(0.57f, 0.56f, 0.53f), new Color(1f, 0.98f, 0.93f)]
        };
        return new GradientTexture1D { Gradient = gradient, Width = 256 };
    }

    /// <summary>Floating pollen by day, fireflies by golden hour.</summary>
    private void BuildMotes()
    {
        var process = new ParticleProcessMaterial
        {
            EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Box,
            EmissionBoxExtents = new Vector3(4f, 0.4f, 4f),
            Gravity = new Vector3(0, 0.02f, 0),
            InitialVelocityMin = 0.02f,
            InitialVelocityMax = 0.08f,
            Spread = 180f,
            TurbulenceEnabled = true,
            TurbulenceNoiseStrength = 0.6f,
            TurbulenceNoiseScale = 2.5f,
            ScaleMin = 0.5f,
            ScaleMax = 1.2f,
            ColorRamp = new GradientTexture1D
            {
                Gradient = new Gradient
                {
                    Offsets = [0f, 0.2f, 0.8f, 1f],
                    Colors = [new Color(1, 1, 1, 0), new Color(1, 1, 1, 1), new Color(1, 1, 1, 1), new Color(1, 1, 1, 0)]
                }
            }
        };
        var dot = new GradientTexture2D
        {
            Gradient = new Gradient { Offsets = [0f, 1f], Colors = [Colors.White, new Color(1, 1, 1, 0)] },
            Fill = GradientTexture2D.FillEnum.Radial,
            FillFrom = new Vector2(0.5f, 0.5f),
            FillTo = new Vector2(0.5f, 0f),
            Width = 32,
            Height = 32
        };
        var material = new StandardMaterial3D
        {
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            BlendMode = BaseMaterial3D.BlendModeEnum.Add,
            BillboardMode = BaseMaterial3D.BillboardModeEnum.Particles,
            AlbedoTexture = dot,
            VertexColorUseAsAlbedo = true,
            AlbedoColor = new Color(1f, 0.95f, 0.75f, 0.5f)
        };
        _motes = new GpuParticles3D
        {
            Name = "Motes",
            Amount = 70,
            Lifetime = 9,
            Preprocess = 9,
            ProcessMaterial = process,
            DrawPass1 = new QuadMesh { Size = new Vector2(0.025f, 0.025f), Material = material },
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            VisibilityAabb = new Aabb(new Vector3(-30, -2, -30), new Vector3(60, 8, 60)),
            Position = new Vector3(0, 0.35f, 0)
        };
        AddChild(_motes);
    }

    private Season _weather = Season.Summer;
    private bool _snowfall;
    private float _motesRadius = 4f;
    private Vector3 _motesCentre;

    public void SetMotesArea(Vector3 centre, float radius)
    {
        _motesCentre = centre;
        _motesRadius = radius;
        ApplyWeatherShape();
    }

    /// <summary>Petals in spring, pollen and fireflies in summer, falling leaves in autumn, snow in winter.</summary>
    public void SetWeather(Season season, ThemeStyle style)
    {
        _weather = season;
        _snowfall = season == Season.Winter && style.Seasons.Snow > 0.3f;
        if (_motes.ProcessMaterial is not ParticleProcessMaterial process || _motes.DrawPass1 is not QuadMesh quad || quad.Material is not StandardMaterial3D material)
            return;

        switch (season)
        {
            case Season.Spring:
                process.Gravity = new Vector3(0.05f, -0.06f, 0);
                quad.Size = new Vector2(0.03f, 0.022f);
                material.BlendMode = BaseMaterial3D.BlendModeEnum.Mix;
                material.AlbedoColor = new Color(Pick(style.Blossoms), 0.9f);
                break;
            case Season.Autumn:
                process.Gravity = new Vector3(0.08f, -0.16f, 0);
                quad.Size = new Vector2(0.035f, 0.025f);
                material.BlendMode = BaseMaterial3D.BlendModeEnum.Mix;
                material.AlbedoColor = new Color("d9682b");
                break;
            case Season.Winter when _snowfall:
                process.Gravity = new Vector3(0.03f, -0.22f, 0);
                quad.Size = new Vector2(0.022f, 0.022f);
                material.BlendMode = BaseMaterial3D.BlendModeEnum.Mix;
                material.AlbedoColor = new Color(1f, 1f, 1f, 0.95f);
                break;
            default:
                process.Gravity = new Vector3(0, 0.02f, 0);
                quad.Size = new Vector2(0.025f, 0.025f);
                material.BlendMode = BaseMaterial3D.BlendModeEnum.Add;
                break;
        }

        ApplyWeatherShape();
        ApplyDaylight(_daylight);

        static Color Pick(Color[] options) => options[(int)(GD.Randi() % (uint)options.Length)];
    }

    private void ApplyWeatherShape()
    {
        if (_motes.ProcessMaterial is not ParticleProcessMaterial process)
            return;
        var falling = _weather is Season.Spring or Season.Autumn || _snowfall;
        var height = falling ? 2.5f : 0.4f;
        process.EmissionBoxExtents = new Vector3(_motesRadius, falling ? 1.2f : 0.4f, _motesRadius);
        _motes.Position = new Vector3(_motesCentre.X, height, _motesCentre.Z);
        var density = _snowfall ? 3.5f : falling ? 1.4f : 0.9f;
        _motes.Amount = Mathf.Clamp(Mathf.RoundToInt(_motesRadius * _motesRadius * density), 24, _snowfall ? 400 : 160);
        _motes.Lifetime = falling ? 14 : 9;
    }

    public void SetMotesEnabled(bool enabled) => _motes.Visible = enabled;

    /// <summary>Keeps the fog band just beyond the island whatever the zoom.</summary>
    public void SetViewDistance(float cameraDistance, float islandRadius)
    {
        _environment.FogDepthBegin = cameraDistance + islandRadius + 6f;
        _environment.FogDepthEnd = cameraDistance + islandRadius + 110f;
    }

    /// <summary>Recolours sea, sky and light for a new island theme.</summary>
    public void ApplyTheme(ThemeStyle style)
    {
        _style = style;
        _oceanMaterial.SetShaderParameter("shallow_color", style.OceanShallow);
        _oceanMaterial.SetShaderParameter("deep_color", style.OceanDeep);
        if (GetNodeOrNull<MeshInstance3D>("Seabed") is { } seabed)
            seabed.Mesh = BuildSeabed(style);
        ApplyDaylight(_daylight);
    }

    private void BuildOcean()
    {
        var water = new ShaderMaterial { Shader = GD.Load<Shader>("res://shaders/water.gdshader") };
        _oceanMaterial = water;
        water.SetShaderParameter("shallow_color", new Color("4fc6c6"));
        water.SetShaderParameter("deep_color", new Color("1e6aa3"));
        water.SetShaderParameter("depth_scale", 2.0f);
        water.SetShaderParameter("foam_width", 0.17f);
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
            Mesh = BuildSeabed(_style),
            MaterialOverride = new StandardMaterial3D { VertexColorUseAsAlbedo = true, Roughness = 1f },
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off
        });
    }

    /// <summary>
    /// Radial seabed in unit space: a sandy shelf right under the island that slopes away into the deep.
    /// The node is scaled with the island so the turquoise shallows always hug the coast.
    /// </summary>
    private static ArrayMesh BuildSeabed(ThemeStyle style)
    {
        var kit = new MeshKit();
        const int segments = 72;
        float[] radii = [0f, 0.6f, 0.9f, 1.1f, 1.35f, 1.7f, 2.2f, 3.2f, 5f, 9f];
        // Gentle bowl: a little shallower under the island, deep blue everywhere else.
        float Height(float r) => Mathf.Lerp(-2.6f, -5f, Mathf.SmoothStep(0.6f, 2.5f, r));
        Color ColorAt(float r)
        {
            var teal = style.ShelfTeal.Darkened(0.05f);
            var deep = style.OceanDeep.Darkened(0.25f);
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
        // Keyframes: morning, midday, golden hour. The theme provides the midday palette.
        static float Key3(float t, float a, float b, float c) => t < 0.4f ? Mathf.Lerp(a, b, t / 0.4f) : Mathf.Lerp(b, c, (t - 0.4f) / 0.6f);
        static Color Col3(float t, Color a, Color b, Color c) => t < 0.4f ? a.Lerp(b, t / 0.4f) : b.Lerp(c, (t - 0.4f) / 0.6f);

        var top = _style.SkyTop;
        var horizon = _style.SkyHorizon;
        var sun = _style.SunColor;
        var golden = new Color("ffd49c");

        _sun.Rotation = new Vector3(
            Mathf.DegToRad(Key3(t, -34f, -56f, -22f)),
            Mathf.DegToRad(Key3(t, 70f, 38f, -12f)),
            0f);
        _sun.LightColor = Col3(t, sun.Lerp(Colors.White, 0.3f), sun, sun.Lerp(golden, 0.85f));
        _sun.LightEnergy = Key3(t, 1.45f, 1.6f, 1.5f);

        var skyTop = Col3(t, top.Lightened(0.05f), top, top.Lerp(new Color("5b8fd0"), 0.4f));
        var skyHorizon = Col3(t, horizon.Lightened(0.05f), horizon, horizon.Lerp(new Color("f6d9b4"), 0.6f));
        _sky.SetShaderParameter("top_color", skyTop);
        _sky.SetShaderParameter("horizon_color", skyHorizon);
        _sky.SetShaderParameter("ground_color", _style.OceanDeep);
        _sky.SetShaderParameter("sun_tint", _sun.LightColor);
        _sky.SetShaderParameter("cloud_color", Col3(t, Colors.White, Colors.White, new Color("fff0dc")));
        _sky.SetShaderParameter("cloud_shade", Col3(t, new Color("cbd8e8"), new Color("c7d5e6"), new Color("e8c3b4")));
        _environment.FogLightColor = skyHorizon;
        _environment.AmbientLightEnergy = Key3(t, 0.85f, 0.8f, 0.78f);

        // Motes become fireflies as the day ends.
        if (_weather == Season.Summer && _motes?.DrawPass1 is QuadMesh { Material: StandardMaterial3D motes })
            motes.AlbedoColor = Col3(t, new Color(1f, 1f, 0.95f, 0.18f), new Color(1f, 0.97f, 0.85f, 0.18f), new Color(1f, 0.85f, 0.45f, 0.75f));
    }

    public void ApplyQuality(bool shadows, bool ssao, bool glow)
    {
        _sun.ShadowEnabled = shadows;
        _environment.SsaoEnabled = ssao;
        _environment.GlowEnabled = glow;
    }
}
