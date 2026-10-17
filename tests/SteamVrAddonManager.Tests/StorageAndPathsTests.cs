using System.IO;
using SteamVrAddonManager.Models;
using SteamVrAddonManager.Services;

namespace SteamVrAddonManager.Tests;

public sealed class StorageAndPathsTests
{
    [Fact]
    public void SteamConfigPaths_ComposesChildPaths()
    {
        var paths = new SteamConfigPaths(@"C:\steam\config");

        Assert.Equal(Path.Combine(@"C:\steam\config", "steamvr.vrsettings"), paths.VrSettingsFile);
        Assert.Equal(Path.Combine(@"C:\steam\config", "appconfig.json"), paths.AppConfigFile);
        Assert.Equal(Path.Combine(@"C:\steam\config", "vrappconfig"), paths.VrappConfigDirectory);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void FromConfigDirectory_ReturnsNullForBlank(string? value)
    {
        Assert.Null(SteamConfigPaths.FromConfigDirectory(value));
    }

    [Fact]
    public void AppSettingsStore_RoundTrips()
    {
        var store = new AppSettingsStore();
        try
        {
            store.Save(new AppSettings { ConfigDirectory = @"C:\steam\config", TaskbarJumpList = true });

            var loaded = store.Load();
            Assert.Equal(@"C:\steam\config", loaded.ConfigDirectory);
            Assert.True(loaded.TaskbarJumpList);
        }
        finally
        {
            try
            {
                File.Delete(PortablePaths.SettingsFile);
            }
            catch
            {
                // Cleanup is best-effort.
            }
        }
    }
}
