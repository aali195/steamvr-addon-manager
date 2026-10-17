using System.Text.Json;
using SteamVrAddonManager.Models;

namespace SteamVrAddonManager.Services;

/// <summary>Loads and saves the portable <c>settings.json</c> next to the executable.</summary>
public sealed class AppSettingsStore
{
    public AppSettings Load()
    {
        try
        {
            if (!File.Exists(PortablePaths.SettingsFile))
                return new AppSettings();

            var json = File.ReadAllText(PortablePaths.SettingsFile);
            return JsonSerializer.Deserialize(json, AppJsonContext.Default.AppSettings) ?? new AppSettings();
        }
        catch
        {
            return new AppSettings();
        }
    }

    public void Save(AppSettings settings)
    {
        try
        {
            Directory.CreateDirectory(PortablePaths.BaseDirectory);
            var json = JsonSerializer.Serialize(settings, AppJsonContext.Default.AppSettings);
            FileStorage.WriteAllTextAtomic(PortablePaths.SettingsFile, json);
        }
        catch
        {
            // Non-fatal: settings simply won't persist.
        }
    }
}
