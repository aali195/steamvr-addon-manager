namespace SteamVrAddonManager.Models;

/// <summary>A saved combination of driver enable states and startup-overlay autolaunch states.</summary>
public sealed class Preset
{
    public string Name { get; set; } = string.Empty;

    public Dictionary<string, PresetDriverState> Drivers { get; set; } = new();

    public Dictionary<string, PresetAppState> Apps { get; set; } = new();
}

/// <summary>A driver's enable state within a preset.</summary>
public sealed class PresetDriverState
{
    public bool Enabled { get; set; }
}

/// <summary>A startup overlay's autolaunch state within a preset.</summary>
public sealed class PresetAppState
{
    public bool AutoLaunch { get; set; }
}
