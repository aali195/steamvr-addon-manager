using CommunityToolkit.Mvvm.ComponentModel;

namespace SteamVrAddonManager.Models;

/// <summary>
/// A SteamVR dashboard overlay that can be told to start with SteamVR, mirrored from the
/// registered manifests and its <c>&lt;app_key&gt;.vrappconfig</c> startup record.
/// </summary>
public partial class StartupApp : ObservableObject
{
    public required string AppKey { get; init; }

    /// <summary>Display name (localized manifest name, or the app key for orphan records).</summary>
    public required string Name { get; init; }

    public string LaunchType { get; init; } = string.Empty;

    /// <summary>True when a startup record exists but no registered manifest declares the app.</summary>
    public bool IsOrphan { get; init; }

    [ObservableProperty]
    private bool _startWithSteamVr;
}
