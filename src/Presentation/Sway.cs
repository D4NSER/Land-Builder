namespace LandBuilder.Presentation;

/// <summary>
/// Vertex alpha on props doubles as wind sway weight and a material class the prop shader reads for seasons:
/// deciduous leaves turn in autumn, grass browns, evergreens stay green, and everything collects snow in winter.
/// </summary>
public static class Sway
{
    public const float Rigid = 0f;

    /// <summary>Walls rise out of the ground when built.</summary>
    public const float Building = 0.03f;

    /// <summary>Roofs drop onto finished walls.</summary>
    public const float Roof = 0.06f;
    public const float Trunk = 0.12f;
    public const float Bush = 0.5f;
    public const float Flower = 0.68f;
    public const float Evergreen = 0.8f;
    public const float Grass = 0.9f;
    public const float Deciduous = 1f;
}
