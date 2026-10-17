using System.IO;
using SteamVrAddonManager.Models;
using SteamVrAddonManager.Services;

namespace SteamVrAddonManager.Tests;

public sealed class PresetRunnerTests
{
    [Fact]
    public void Apply_WritesDriverAndOverlayState()
    {
        using var dir = new TempDir();
        var vrapp = Path.Combine(dir.Path, "vrappconfig");
        Directory.CreateDirectory(vrapp);

        var settings = dir.File("steamvr.vrsettings", """{ "driver_A": { "enable": false } }""");
        var manifest = dir.File("m.vrmanifest",
            """{ "applications": [ { "app_key": "ov", "is_dashboard_overlay": true } ] }""");
        dir.File("appconfig.json", $$"""{ "manifest_paths": [ "{{manifest.Replace('\\', '/')}}" ] }""");
        File.WriteAllText(Path.Combine(vrapp, "ov.vrappconfig"), """{ "autolaunch": false }""");

        var store = new PresetStore();
        var name = "t-" + Guid.NewGuid().ToString("N");
        store.Save(new Preset
        {
            Name = name,
            Drivers = { ["A"] = new PresetDriverState { Enabled = true } },
            Apps = { ["ov"] = new PresetAppState { AutoLaunch = true } },
        });

        try
        {
            var code = PresetRunner.Apply(name, dir.Path, logPath: null);

            Assert.Equal(PresetRunner.Success, code);
            Assert.Contains("\"enable\": true", File.ReadAllText(settings));
            Assert.Contains("\"autolaunch\": true", File.ReadAllText(Path.Combine(vrapp, "ov.vrappconfig")));
        }
        finally
        {
            store.Delete(name);
        }
    }

    [Fact]
    public void Apply_MissingPreset_ReturnsPresetNotFound()
    {
        using var dir = new TempDir();
        Assert.Equal(PresetRunner.PresetNotFound, PresetRunner.Apply("missing-" + Guid.NewGuid().ToString("N"), dir.Path, null));
    }

    [Fact]
    public void Apply_MissingConfigFolder_ReturnsConfigFolderNotFound()
    {
        var missing = Path.Combine(Path.GetTempPath(), "missing-" + Guid.NewGuid().ToString("N"));
        Assert.Equal(PresetRunner.ConfigFolderNotFound, PresetRunner.Apply("anything", missing, null));
    }
}
