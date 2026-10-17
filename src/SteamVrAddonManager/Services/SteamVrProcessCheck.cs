using System.Diagnostics;

namespace SteamVrAddonManager.Services;

/// <summary>Best-effort detection of whether SteamVR is currently running.</summary>
public static class SteamVrProcessCheck
{
    private static readonly string[] ProcessNames = { "vrserver", "vrmonitor" };

    public static bool IsRunning
    {
        get
        {
            foreach (var name in ProcessNames)
            {
                if (IsProcessRunning(name))
                    return true;
            }

            return false;
        }
    }

    private static bool IsProcessRunning(string processName)
    {
        try
        {
            var processes = Process.GetProcessesByName(processName);
            try
            {
                return processes.Length > 0;
            }
            finally
            {
                foreach (var process in processes)
                    process.Dispose();
            }
        }
        catch
        {
            // Process enumeration is best-effort; treat failures as "not running".
            return false;
        }
    }
}
