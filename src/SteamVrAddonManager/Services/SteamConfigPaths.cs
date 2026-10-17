namespace SteamVrAddonManager.Services;

/// <summary>
/// The Steam <c>config</c> folder and the files SteamVR keeps there. This folder is the unit the
/// tool manages: it holds <c>steamvr.vrsettings</c> (drivers), <c>appconfig.json</c> + manifests
/// (startup overlays) and <c>vrappconfig</c> (overlay autolaunch).
/// </summary>
public sealed class SteamConfigPaths
{
    private const string VrSettingsFileName = "steamvr.vrsettings";

    public SteamConfigPaths(string configDirectory) => ConfigDirectory = configDirectory;

    public string ConfigDirectory { get; }

    public string VrSettingsFile => Path.Combine(ConfigDirectory, VrSettingsFileName);

    public string AppConfigFile => Path.Combine(ConfigDirectory, "appconfig.json");

    public string VrappConfigDirectory => Path.Combine(ConfigDirectory, "vrappconfig");

    public static SteamConfigPaths? FromConfigDirectory(string? configDirectory) =>
        string.IsNullOrWhiteSpace(configDirectory) ? null : new SteamConfigPaths(configDirectory);
}
