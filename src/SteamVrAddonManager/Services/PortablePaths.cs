namespace SteamVrAddonManager.Services;

/// <summary>
/// Resolves the portable data folder: the executable's own directory when writable, otherwise a
/// per-user fallback. Used so settings and presets travel with the exe.
/// </summary>
public static class PortablePaths
{
    private static string? _baseDir;

    public static string BaseDirectory => _baseDir ??= ResolveBaseDirectory();

    public static string PresetsDirectory => Path.Combine(BaseDirectory, "presets");

    public static string SettingsFile => Path.Combine(BaseDirectory, "settings.json");

    private static string ResolveBaseDirectory()
    {
        var exeDir = AppContext.BaseDirectory;
        if (IsWritable(exeDir))
            return exeDir;

        var fallback = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "SteamVrAddonManager");
        Directory.CreateDirectory(fallback);
        return fallback;
    }

    private static bool IsWritable(string directory)
    {
        try
        {
            Directory.CreateDirectory(directory);
            var probe = Path.Combine(directory, ".write-probe");
            File.WriteAllText(probe, string.Empty);
            File.Delete(probe);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
