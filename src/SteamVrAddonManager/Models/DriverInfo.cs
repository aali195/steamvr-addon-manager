using CommunityToolkit.Mvvm.ComponentModel;

namespace SteamVrAddonManager.Models;

/// <summary>A single SteamVR driver, mirrored from a <c>driver_*</c> section of the settings file.</summary>
public partial class DriverInfo : ObservableObject
{
    /// <summary>Display name (the section key without the <c>driver_</c> prefix).</summary>
    public required string Name { get; init; }

    /// <summary>The full top-level JSON key, e.g. <c>driver_VirtualDesktop</c>.</summary>
    public required string SectionKey { get; init; }

    [ObservableProperty]
    private bool _isEnabled;

    [ObservableProperty]
    private bool _isBlocked;
}
