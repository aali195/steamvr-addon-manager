using SteamVrAddonManager.Models;

namespace SteamVrAddonManager.Services;

/// <summary>
/// Applies a saved preset to the SteamVR configuration without any UI. Used by
/// <c>SteamVrAddonManager.exe --apply "&lt;preset&gt;"</c> so another process can switch profiles silently.
/// </summary>
internal static class PresetRunner
{
    public const int Success = 0;
    public const int InvalidArguments = 1;
    public const int PresetNotFound = 2;
    public const int ConfigFolderNotFound = 3;
    public const int ApplyFailed = 4;

    public static int Apply(string presetName, string? configDirectoryOverride, string? logPath)
    {
        if (string.IsNullOrWhiteSpace(presetName))
        {
            Log(logPath, "No preset name supplied.");
            return InvalidArguments;
        }

        var configDirectory = ResolveConfigDirectory(configDirectoryOverride);
        if (configDirectory is null || !Directory.Exists(configDirectory))
        {
            Log(logPath, $"Steam config folder not found: {configDirectory ?? "(none)"}");
            return ConfigFolderNotFound;
        }

        var paths = new SteamConfigPaths(configDirectory);

        var preset = new PresetStore().Load(presetName);
        if (preset is null)
        {
            Log(logPath, $"Preset not found: '{presetName}'");
            return PresetNotFound;
        }

        try
        {
            var driverChanges = ApplyDrivers(paths, preset);
            var overlayChanges = ApplyOverlays(paths, preset);

            var warning = SteamVrProcessCheck.IsRunning ? " (warning: SteamVR is running)" : string.Empty;
            Log(logPath,
                $"Applied preset '{preset.Name}': {driverChanges} driver change(s), {overlayChanges} overlay change(s).{warning}");

            return Success;
        }
        catch (Exception ex)
        {
            Log(logPath, $"Failed to apply preset '{presetName}': {ex.Message}");
            return ApplyFailed;
        }
    }

    private static string? ResolveConfigDirectory(string? overridePath)
    {
        if (!string.IsNullOrWhiteSpace(overridePath))
            return overridePath;

        var remembered = new AppSettingsStore().Load().ConfigDirectory;
        return string.IsNullOrWhiteSpace(remembered) ? SteamLocator.AutoDetectConfigDirectory() : remembered;
    }

    private static int ApplyDrivers(SteamConfigPaths paths, Preset preset)
    {
        if (preset.Drivers.Count == 0)
            return 0;

        var vrSettings = paths.VrSettingsFile;
        if (!File.Exists(vrSettings))
            return 0;

        var document = VrSettingsDocument.Load(vrSettings);
        var drivers = document.GetDrivers();
        var changed = 0;

        foreach (var driver in drivers)
        {
            if (!preset.Drivers.TryGetValue(driver.Name, out var state))
                continue;

            if (driver.IsEnabled != state.Enabled)
                changed++;

            driver.IsEnabled = state.Enabled;
        }

        document.ApplyDrivers(drivers);
        document.Save();
        return changed;
    }

    private static int ApplyOverlays(SteamConfigPaths paths, Preset preset)
    {
        if (preset.Apps.Count == 0)
            return 0;

        var catalog = new StartupAppCatalog(paths);
        var changed = new List<StartupApp>();

        foreach (var app in catalog.Load())
        {
            if (!preset.Apps.TryGetValue(app.AppKey, out var state) || app.StartWithSteamVr == state.AutoLaunch)
                continue;

            app.StartWithSteamVr = state.AutoLaunch;
            changed.Add(app);
        }

        catalog.Save(changed);
        return changed.Count;
    }

    private static void Log(string? logPath, string message)
    {
        if (string.IsNullOrWhiteSpace(logPath))
            return;

        try
        {
            var directory = Path.GetDirectoryName(logPath);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            File.AppendAllText(logPath, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} {message}{Environment.NewLine}");
        }
        catch
        {
            // Logging must never affect the result.
        }
    }
}
