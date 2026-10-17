namespace SteamVrAddonManager.Models;

/// <summary>Persisted application preferences (stored in <c>settings.json</c>).</summary>
public sealed class AppSettings
{
    /// <summary>The Steam <c>config</c> folder that was explicitly chosen by the user, if any.</summary>
    public string? ConfigDirectory { get; set; }

    /// <summary>Whether the presets are published as taskbar Jump List "Tasks".</summary>
    public bool TaskbarJumpList { get; set; }
}
