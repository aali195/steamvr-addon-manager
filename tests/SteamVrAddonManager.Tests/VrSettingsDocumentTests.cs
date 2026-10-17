using System.IO;
using SteamVrAddonManager.Services;

namespace SteamVrAddonManager.Tests;

public sealed class VrSettingsDocumentTests
{
    [Fact]
    public void GetDrivers_ReadsEnabledBlockedAndImplicitEnable_AndSorts()
    {
        using var dir = new TempDir();
        var file = dir.File("steamvr.vrsettings",
            """
            {
              "driver_Beta": { "blocked_by_safe_mode": true },
              "driver_Alpha": { "enable": false },
              "steamvr": { "x": 1 }
            }
            """);

        var drivers = VrSettingsDocument.Load(file).GetDrivers();

        Assert.Equal(2, drivers.Count);
        Assert.Equal("Alpha", drivers[0].Name);
        Assert.False(drivers[0].IsEnabled);
        Assert.Equal("Beta", drivers[1].Name);
        Assert.True(drivers[1].IsBlocked);
        Assert.True(drivers[1].IsEnabled); // absent "enable" means implicitly enabled
    }

    [Fact]
    public void ApplyAndSave_WritesOnlyEnable_AndPreservesEverythingElse()
    {
        using var dir = new TempDir();
        var file = dir.File("steamvr.vrsettings",
            """
            { "driver_A": { "blocked_by_safe_mode": true, "enable": false }, "GpuSpeed": { "gpuSpeed0": 1 } }
            """);

        var document = VrSettingsDocument.Load(file);
        var drivers = document.GetDrivers();
        foreach (var driver in drivers)
            driver.IsEnabled = true; // leave blocked state alone

        document.ApplyDrivers(drivers);
        document.Save();

        var json = File.ReadAllText(file);
        Assert.Contains("\"enable\": true", json);
        Assert.Contains("\"blocked_by_safe_mode\": true", json);
        Assert.Contains("gpuSpeed0", json);
    }

    [Fact]
    public void ApplyUnblocked_RemovesTheBlockedKey()
    {
        using var dir = new TempDir();
        var file = dir.File("steamvr.vrsettings",
            """{ "driver_A": { "blocked_by_safe_mode": true } }""");

        var document = VrSettingsDocument.Load(file);
        var drivers = document.GetDrivers();
        drivers[0].IsBlocked = false;
        document.ApplyDrivers(drivers);
        document.Save();

        Assert.DoesNotContain("blocked_by_safe_mode", File.ReadAllText(file));
    }
}
