using System;
using System.IO;
using System.Text.Json;
using Godot;

namespace LandBuilder.Presentation;

/// <summary>Player preferences, stored as JSON in user://settings.json.</summary>
public sealed class GameSettings
{
    public float MasterVolume { get; set; } = 0.9f;
    public float MusicVolume { get; set; } = 0.7f;
    public float SfxVolume { get; set; } = 0.9f;
    public float AmbientVolume { get; set; } = 0.7f;
    public bool Shadows { get; set; } = true;
    public bool AmbientOcclusion { get; set; } = true;
    public bool Bloom { get; set; } = true;
    public bool DepthOfField { get; set; } = true;
    public bool Particles { get; set; } = true;
    public bool EdgeHints { get; set; } = true;
    public bool ScreenShake { get; set; } = true;
    public bool Fullscreen { get; set; }
    public bool TutorialDone { get; set; }

    private static string FilePath => ProjectSettings.GlobalizePath("user://settings.json");

    public static GameSettings Load()
    {
        try
        {
            if (File.Exists(FilePath))
                return JsonSerializer.Deserialize<GameSettings>(File.ReadAllText(FilePath)) ?? new GameSettings();
        }
        catch (Exception ex)
        {
            GD.PushWarning($"Settings unreadable, using defaults: {ex.Message}");
        }

        return new GameSettings();
    }

    public void Save()
    {
        try
        {
            File.WriteAllText(FilePath, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (Exception ex)
        {
            GD.PushWarning($"Could not save settings: {ex.Message}");
        }
    }
}
