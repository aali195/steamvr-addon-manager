using SteamVrAddonManager.Models;
using SteamVrAddonManager.Services;

namespace SteamVrAddonManager.Tests;

public sealed class PresetStoreTests
{
    private static string UniqueName() => "t-" + Guid.NewGuid().ToString("N");

    [Fact]
    public void SaveLoadListDelete_RoundTrips()
    {
        var store = new PresetStore();
        var name = UniqueName();
        try
        {
            store.Save(new Preset { Name = name, Drivers = { ["A"] = new PresetDriverState { Enabled = true } } });

            Assert.Contains(name, store.ListPresets());
            Assert.True(store.Load(name)!.Drivers["A"].Enabled);

            store.Delete(name);
            Assert.DoesNotContain(name, store.ListPresets());
        }
        finally
        {
            store.Delete(name);
        }
    }

    [Fact]
    public void Rename_MovesPresetAndKeepsContents()
    {
        var store = new PresetStore();
        var oldName = UniqueName();
        var newName = oldName + "-r";
        try
        {
            store.Save(new Preset { Name = oldName, Apps = { ["ov"] = new PresetAppState { AutoLaunch = true } } });

            Assert.True(store.Rename(oldName, newName));
            Assert.Contains(newName, store.ListPresets());
            Assert.DoesNotContain(oldName, store.ListPresets());
            Assert.True(store.Load(newName)!.Apps["ov"].AutoLaunch);
        }
        finally
        {
            store.Delete(oldName);
            store.Delete(newName);
        }
    }

    [Fact]
    public void Duplicate_MakesUniqueCopy()
    {
        var store = new PresetStore();
        var name = UniqueName();
        string? copy = null;
        try
        {
            store.Save(new Preset { Name = name });

            copy = store.Duplicate(name);
            Assert.Equal(name + " copy", copy);
            Assert.Contains(copy, store.ListPresets());
        }
        finally
        {
            store.Delete(name);
            if (copy is not null)
                store.Delete(copy);
        }
    }
}
