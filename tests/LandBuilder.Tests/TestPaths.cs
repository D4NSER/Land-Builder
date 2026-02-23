namespace LandBuilder.Tests;

internal static class TestPaths
{
    public static string ObjectivesJson => Resolve("data", "objectives", "mvp2_objectives.json");
    public static string V2Fixture => Resolve("fixtures", "saves", "v2_mvp1_save.json");

    private static string Resolve(params string[] relativeParts)
    {
        var candidate = Path.Combine(AppContext.BaseDirectory, Path.Combine(relativeParts));
        if (File.Exists(candidate)) return candidate;

        // Fallback for running from solution directory.
        candidate = Path.Combine(relativeParts);
        if (File.Exists(candidate)) return candidate;

        throw new FileNotFoundException($"Required test file not found: {Path.Combine(relativeParts)}");
    }
}
