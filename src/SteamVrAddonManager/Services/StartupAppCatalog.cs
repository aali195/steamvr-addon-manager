using System.Text;
using System.Text.Json.Nodes;
using SteamVrAddonManager.Models;

namespace SteamVrAddonManager.Services;

/// <summary>
/// Builds the list of startup overlays from the manifests registered in <c>appconfig.json</c>
/// and their <c>vrappconfig</c> startup records, and writes autolaunch changes back.
/// </summary>
public sealed class StartupAppCatalog
{
    private const string OrphanOverlayKeyPrefix = "steam.overlay.";

    private readonly SteamConfigPaths _paths;
    private readonly VrappConfigStore _store;

    public StartupAppCatalog(SteamConfigPaths paths)
    {
        _paths = paths;
        _store = new VrappConfigStore(paths.VrappConfigDirectory);
    }

    public IReadOnlyList<StartupApp> Load()
    {
        var apps = new List<StartupApp>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var manifestPath in ReadManifestPaths())
        {
            if (!File.Exists(manifestPath))
                continue;

            IReadOnlyList<ManifestApp> parsed;
            try
            {
                parsed = VrManifestReader.Read(manifestPath);
            }
            catch
            {
                continue;
            }

            foreach (var app in parsed)
            {
                if (!app.IsDashboardOverlay || !seen.Add(app.AppKey))
                    continue;

                apps.Add(CreateApp(app.AppKey, app.Name, app.LaunchType, isOrphan: false));
            }
        }

        // Startup records for overlays whose manifest is no longer registered stay controllable.
        foreach (var appKey in _store.EnumerateAppKeys())
        {
            if (!appKey.StartsWith(OrphanOverlayKeyPrefix, StringComparison.OrdinalIgnoreCase) || !seen.Add(appKey))
                continue;

            apps.Add(CreateApp(appKey, appKey, launchType: string.Empty, isOrphan: true));
        }

        return apps.OrderBy(static app => app.Name, StringComparer.OrdinalIgnoreCase).ToList();
    }

    public void Save(IEnumerable<StartupApp> apps)
    {
        foreach (var app in apps)
            _store.SetAutoLaunch(app.AppKey, app.StartWithSteamVr);
    }

    /// <summary>Backs up and deletes an app's startup record.</summary>
    public void RemoveRecord(string appKey) => _store.DeleteAutoLaunch(appKey);

    private StartupApp CreateApp(string appKey, string name, string launchType, bool isOrphan)
    {
        return new StartupApp
        {
            AppKey = appKey,
            Name = name,
            LaunchType = launchType,
            IsOrphan = isOrphan,
            StartWithSteamVr = _store.GetAutoLaunch(appKey) ?? false,
        };
    }

    private IEnumerable<string> ReadManifestPaths()
    {
        if (!File.Exists(_paths.AppConfigFile))
            yield break;

        JsonNode? node;
        try
        {
            node = JsonNode.Parse(File.ReadAllText(_paths.AppConfigFile, Encoding.UTF8));
        }
        catch
        {
            yield break;
        }

        if (node is not JsonObject root || root["manifest_paths"] is not JsonArray paths)
            yield break;

        foreach (var entry in paths)
        {
            if (entry is JsonValue value && value.TryGetValue<string>(out var path) && !string.IsNullOrWhiteSpace(path))
                yield return path;
        }
    }
}
