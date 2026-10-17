namespace SteamVrAddonManager.Services;

/// <summary>Locates Steam's <c>config</c> folder by reading the Steam install path from the registry.</summary>
public static class SteamLocator
{
    /// <summary>
    /// Returns the first existing config directory (the folder holding <c>steamvr.vrsettings</c>,
    /// <c>appconfig.json</c> and <c>vrappconfig</c>), or the most likely path when none exists yet.
    /// </summary>
    public static string? AutoDetectConfigDirectory()
    {
        foreach (var candidate in GetCandidateDirectories())
        {
            if (Directory.Exists(candidate))
                return candidate;
        }

        return GetCandidateDirectories().FirstOrDefault();
    }

    private static IEnumerable<string> GetCandidateDirectories()
    {
        foreach (var steamDir in GetSteamDirectories())
            yield return Path.Combine(steamDir, "config");

        // Legacy fallback for pre-Steam-config installs.
        var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (!string.IsNullOrEmpty(local))
            yield return Path.Combine(local, "openvr");
    }

    private static IEnumerable<string> GetSteamDirectories()
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var raw in EnumerateRegistryPaths())
        {
            var normalized = Normalize(raw);
            if (normalized is not null && seen.Add(normalized))
                yield return normalized;
        }
    }

    private static IEnumerable<string?> EnumerateRegistryPaths()
    {
        yield return ReadRegistry(@"HKEY_CURRENT_USER\Software\Valve\Steam", "SteamPath");
        yield return ReadRegistry(@"HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\Valve\Steam", "InstallPath");
        yield return ReadRegistry(@"HKEY_LOCAL_MACHINE\SOFTWARE\Valve\Steam", "InstallPath");
    }

    private static string? Normalize(string? directory)
    {
        if (string.IsNullOrWhiteSpace(directory))
            return null;

        var normalized = directory
            .Replace('/', Path.DirectorySeparatorChar)
            .Replace('\\', Path.DirectorySeparatorChar)
            .TrimEnd(Path.DirectorySeparatorChar);

        return normalized.Length == 0 ? null : normalized;
    }

    private static string? ReadRegistry(string keyPath, string valueName)
    {
        try
        {
            return Microsoft.Win32.Registry.GetValue(keyPath, valueName, null) as string;
        }
        catch
        {
            return null;
        }
    }
}
