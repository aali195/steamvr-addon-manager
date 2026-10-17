using System.IO;
using SteamVrAddonManager.Services;

namespace SteamVrAddonManager.Tests;

public sealed class OverlayTests
{
    [Fact]
    public void ManifestReader_AcceptsBooleanNumberAndStringOverlayFlags()
    {
        using var dir = new TempDir();
        var manifest = dir.File("m.vrmanifest",
            """
            { "applications": [
              { "app_key": "a", "is_dashboard_overlay": true, "strings": { "en_us": { "name": "Alpha" } } },
              { "app_key": "b", "is_dashboard_overlay": 1 },
              { "app_key": "c", "is_dashboard_overlay": "true" },
              { "app_key": "d" }
            ] }
            """);

        var apps = VrManifestReader.Read(manifest);

        Assert.Equal(4, apps.Count);
        Assert.All(apps.Take(3), app => Assert.True(app.IsDashboardOverlay));
        Assert.False(apps[3].IsDashboardOverlay);
        Assert.Equal("Alpha", apps[0].Name);
        Assert.Equal("b", apps[1].Name); // falls back to the app key
    }

    [Fact]
    public void VrappConfigStore_RoundTrips_AndPreservesOtherKeys()
    {
        using var dir = new TempDir();
        var store = new VrappConfigStore(dir.Path);

        Assert.Null(store.GetAutoLaunch("x"));

        File.WriteAllText(Path.Combine(dir.Path, "x.vrappconfig"),
            """{ "last_launch_time": "42", "extra": "keep" }""");

        store.SetAutoLaunch("x", true);

        Assert.True(store.GetAutoLaunch("x"));
        var json = File.ReadAllText(Path.Combine(dir.Path, "x.vrappconfig"));
        Assert.Contains("\"42\"", json);
        Assert.Contains("keep", json);
        Assert.Contains("autolaunch", json);
    }

    [Fact]
    public void VrappConfigStore_Delete_RemovesFileAndKeepsBackup()
    {
        using var dir = new TempDir();
        var store = new VrappConfigStore(dir.Path);
        File.WriteAllText(Path.Combine(dir.Path, "x.vrappconfig"), """{ "autolaunch": true }""");

        store.DeleteAutoLaunch("x");

        Assert.False(File.Exists(Path.Combine(dir.Path, "x.vrappconfig")));
        Assert.Single(Directory.GetFiles(dir.Path, "x.vrappconfig.*.bak"));
    }

    [Fact]
    public void Catalog_ListsOverlaysAndOrphans_ButExcludesGames()
    {
        using var dir = new TempDir();
        var vrapp = Path.Combine(dir.Path, "vrappconfig");
        Directory.CreateDirectory(vrapp);

        var manifest = dir.File("m.vrmanifest",
            """
            { "applications": [
              { "app_key": "ov", "is_dashboard_overlay": true },
              { "app_key": "game" }
            ] }
            """);
        dir.File("appconfig.json",
            $$"""{ "manifest_paths": [ "{{manifest.Replace('\\', '/')}}" ] }""");
        File.WriteAllText(Path.Combine(vrapp, "steam.overlay.777.vrappconfig"), """{ "autolaunch": true }""");

        var apps = new StartupAppCatalog(new SteamConfigPaths(dir.Path)).Load();

        Assert.Contains(apps, app => app.AppKey == "ov");
        Assert.Contains(apps, app => app.AppKey == "steam.overlay.777" && app.IsOrphan);
        Assert.DoesNotContain(apps, app => app.AppKey == "game");
    }
}
