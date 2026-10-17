using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace SteamVrAddonManager.Services;

/// <summary>
/// Reads and writes the per-application <c>&lt;app_key&gt;.vrappconfig</c> files that hold the
/// <c>autolaunch</c> ("start with SteamVR") flag. Other keys, such as <c>last_launch_time</c>,
/// are preserved.
/// </summary>
public sealed class VrappConfigStore
{
    private const string AutoLaunchKey = "autolaunch";
    private const string LastLaunchTimeKey = "last_launch_time";

    private readonly string _directory;

    public VrappConfigStore(string vrappConfigDirectory) => _directory = vrappConfigDirectory;

    public bool? GetAutoLaunch(string appKey)
    {
        var file = FilePath(appKey);
        if (!File.Exists(file))
            return null;

        try
        {
            if (JsonNode.Parse(File.ReadAllText(file, Encoding.UTF8)) is JsonObject root &&
                root[AutoLaunchKey] is JsonValue value &&
                value.TryGetValue<bool>(out var enabled))
            {
                return enabled;
            }
        }
        catch
        {
            // Malformed or unreadable: treat as unset.
        }

        return null;
    }

    public void SetAutoLaunch(string appKey, bool enabled)
    {
        Directory.CreateDirectory(_directory);

        var file = FilePath(appKey);
        var root = ReadObject(file);

        root[AutoLaunchKey] = enabled;
        if (!root.ContainsKey(LastLaunchTimeKey))
            root[LastLaunchTimeKey] = "0";

        FileStorage.Backup(file);
        FileStorage.WriteAllTextAtomic(file, root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
    }

    public IEnumerable<string> EnumerateAppKeys()
    {
        if (!Directory.Exists(_directory))
            return Array.Empty<string>();

        return Directory.EnumerateFiles(_directory, "*.vrappconfig")
            .Select(Path.GetFileNameWithoutExtension)
            .Where(static name => !string.IsNullOrEmpty(name))
            .Select(static name => name!)
            .ToList();
    }

    /// <summary>Backs up and deletes an app's startup record. No-op when the record is absent.</summary>
    public void DeleteAutoLaunch(string appKey)
    {
        var file = FilePath(appKey);
        if (!File.Exists(file))
            return;

        FileStorage.Backup(file);
        File.Delete(file);
    }

    private static JsonObject ReadObject(string file)
    {
        if (!File.Exists(file))
            return new JsonObject();

        try
        {
            if (JsonNode.Parse(File.ReadAllText(file, Encoding.UTF8)) is JsonObject root)
                return root;
        }
        catch
        {
            // Malformed: start from a clean object.
        }

        return new JsonObject();
    }

    private string FilePath(string appKey) => Path.Combine(_directory, appKey + ".vrappconfig");
}
