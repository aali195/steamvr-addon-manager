namespace SteamVrAddonManager.ViewModels;

/// <summary>Lets the driver view model fold startup-overlay state into presets.</summary>
public interface IStartupAppState
{
    /// <summary>Snapshot of every overlay's autolaunch state, keyed by app key.</summary>
    IReadOnlyDictionary<string, bool> CaptureAutoLaunch();

    /// <summary>Applies autolaunch states from a preset to the currently loaded overlays.</summary>
    void ApplyAutoLaunch(IReadOnlyDictionary<string, bool> states);
}
