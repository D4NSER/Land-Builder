using Godot;
using LandBuilder.Domain;

namespace LandBuilder.Presentation;

public enum Season
{
    Spring,
    Summer,
    Autumn,
    Winter
}

/// <summary>
/// Drives the season shader globals. Seasons advance with each score milestone; every theme shows them in its own way
/// (Nordic winters bury everything in snow, Sunlit Plains has a dry season instead).
/// </summary>
public partial class SeasonDirector : Node
{
    private float _snow;
    private float _autumn;
    private float _spring;
    private float _dry;
    private float _freeze;
    private (float Snow, float Autumn, float Spring, float Dry, float Freeze) _target;

    public Season Current { get; private set; } = Season.Spring;

    public static Season ForMilestones(int milestonesReached) => (Season)(milestonesReached % 4);

    public static string DisplayName(Season season, ThemeStyle style) => season switch
    {
        Season.Spring => style.Theme == WorldTheme.Japanese ? "Sakura spring" : "Spring",
        Season.Summer => "Summer",
        Season.Autumn => "Autumn",
        _ => style.Seasons.Snow > 0.3f ? "Winter" : style.Seasons.Dry > 0.5f ? "Dry season" : "Rainy season"
    };

    public static string Tagline(Season season, ThemeStyle style) => season switch
    {
        Season.Spring => style.Theme == WorldTheme.Japanese ? "Cherry blossom drifts on the breeze" : "Fresh green shoots everywhere",
        Season.Summer => "Long golden days",
        Season.Autumn => style.Seasons.Autumn > 0.5f ? "The woods turn red and gold" : "The air cools a little",
        _ => style.Seasons.Snow > 0.3f ? "Snow settles and the lakes freeze" : style.Seasons.Dry > 0.5f ? "The grass turns to straw" : "Warm rain over the island"
    };

    public static Color Tint(Season season) => season switch
    {
        Season.Spring => new Color("e88fb0"),
        Season.Summer => new Color("6cc070"),
        Season.Autumn => new Color("e07b39"),
        _ => new Color("8fb3cf")
    };

    public void Set(Season season, ThemeStyle style, bool instant)
    {
        Current = season;
        var p = style.Seasons;
        _target = season switch
        {
            Season.Spring => (0f, 0f, p.Spring, 0f, 0f),
            Season.Summer => (0f, 0f, 0f, p.Dry * 0.5f, 0f),
            Season.Autumn => (0f, p.Autumn, 0f, p.Dry * 0.6f, 0f),
            _ => (p.Snow, p.Autumn * 0.2f, 0f, p.Dry, p.Snow > 0.3f ? 1f : 0f)
        };

        if (instant)
            (_snow, _autumn, _spring, _dry, _freeze) = _target;
        Push();
    }

    public override void _Process(double delta)
    {
        var k = Ease.Damp(0.9f, (float)delta);
        _snow = Mathf.Lerp(_snow, _target.Snow, k);
        _autumn = Mathf.Lerp(_autumn, _target.Autumn, k);
        _spring = Mathf.Lerp(_spring, _target.Spring, k);
        _dry = Mathf.Lerp(_dry, _target.Dry, k);
        _freeze = Mathf.Lerp(_freeze, _target.Freeze, k * 0.7f);
        Push();
    }

    private void Push()
    {
        RenderingServer.GlobalShaderParameterSet("season_snow", _snow);
        RenderingServer.GlobalShaderParameterSet("season_autumn", _autumn);
        RenderingServer.GlobalShaderParameterSet("season_spring", _spring);
        RenderingServer.GlobalShaderParameterSet("season_dry", _dry);
        RenderingServer.GlobalShaderParameterSet("season_freeze", _freeze);
    }
}
