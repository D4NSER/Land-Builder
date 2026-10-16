using Godot;

namespace LandBuilder.Presentation;

/// <summary>
/// Critically/under-damped springs. Everything that moves in the game is driven by these so motion has weight:
/// tiles drop and settle, the hand tile lags and tilts, UI numbers overshoot slightly.
/// </summary>
public struct Spring
{
    public float Value;
    public float Velocity;
    public float Target;
    public float Stiffness;
    public float Damping;

    public Spring(float value, float stiffness = 180f, float damping = 14f)
    {
        Value = value;
        Target = value;
        Velocity = 0f;
        Stiffness = stiffness;
        Damping = damping;
    }

    public void Step(float delta)
    {
        // Semi-implicit Euler in small sub-steps stays stable at low frame rates.
        var steps = Mathf.Max(1, Mathf.CeilToInt(delta / (1f / 240f)));
        var h = delta / steps;
        for (var i = 0; i < steps; i++)
        {
            var force = (Stiffness * (Target - Value)) - (Damping * Velocity);
            Velocity += force * h;
            Value += Velocity * h;
        }
    }

    public void Snap(float value)
    {
        Value = value;
        Target = value;
        Velocity = 0f;
    }

    public readonly bool IsSettled(float epsilon = 0.001f) =>
        Mathf.Abs(Target - Value) < epsilon && Mathf.Abs(Velocity) < epsilon * 10f;
}

public struct Spring3
{
    public Vector3 Value;
    public Vector3 Velocity;
    public Vector3 Target;
    public float Stiffness;
    public float Damping;

    public Spring3(Vector3 value, float stiffness = 180f, float damping = 14f)
    {
        Value = value;
        Target = value;
        Velocity = Vector3.Zero;
        Stiffness = stiffness;
        Damping = damping;
    }

    public void Step(float delta)
    {
        var steps = Mathf.Max(1, Mathf.CeilToInt(delta / (1f / 240f)));
        var h = delta / steps;
        for (var i = 0; i < steps; i++)
        {
            var force = (Stiffness * (Target - Value)) - (Damping * Velocity);
            Velocity += force * h;
            Value += Velocity * h;
        }
    }

    public void Snap(Vector3 value)
    {
        Value = value;
        Target = value;
        Velocity = Vector3.Zero;
    }
}

public static class Ease
{
    public static float OutBack(float t, float overshoot = 1.70158f)
    {
        var c3 = overshoot + 1f;
        var u = t - 1f;
        return 1f + (c3 * u * u * u) + (overshoot * u * u);
    }

    public static float OutCubic(float t) => 1f - Mathf.Pow(1f - t, 3f);

    public static float InOutSine(float t) => -(Mathf.Cos(Mathf.Pi * t) - 1f) / 2f;

    /// <summary>Exponential smoothing factor that is frame-rate independent.</summary>
    public static float Damp(float sharpness, float delta) => 1f - Mathf.Exp(-sharpness * delta);
}
