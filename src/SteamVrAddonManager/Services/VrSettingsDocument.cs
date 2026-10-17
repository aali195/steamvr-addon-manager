using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using SteamVrAddonManager.Models;

namespace SteamVrAddonManager.Services;

/// <summary>
/// Reads and mutates a <c>steamvr.vrsettings</c> file while preserving every key it does not
/// explicitly manage. Only the enabled state of drivers is written; blocked state is SteamVR's.
/// </summary>
public sealed class VrSettingsDocument
{
    private const string DriverPrefix = "driver_";
    private const string EnableKey = "enable";
    private const string BlockedKey = "blocked_by_safe_mode";

    private readonly JsonObject _root;

    private VrSettingsDocument(JsonObject root, string filePath)
    {
        _root = root;
        FilePath = filePath;
    }

    public string FilePath { get; }

    public static VrSettingsDocument Load(string filePath)
    {
        var json = File.ReadAllText(filePath, Encoding.UTF8);
        var documentOptions = new JsonDocumentOptions
        {
            // SteamVR ships commented / trailing-comma defaults, so be lenient when parsing.
            CommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
        };

        var node = JsonNode.Parse(json, documentOptions: documentOptions);
        if (node is not JsonObject root)
            throw new InvalidDataException("The settings file is not a JSON object.");

        return new VrSettingsDocument(root, filePath);
    }

    public IReadOnlyList<DriverInfo> GetDrivers()
    {
        var drivers = new List<DriverInfo>();

        foreach (var entry in _root)
        {
            if (entry.Value is not JsonObject section ||
                !entry.Key.StartsWith(DriverPrefix, StringComparison.Ordinal))
            {
                continue;
            }

            drivers.Add(new DriverInfo
            {
                Name = entry.Key[DriverPrefix.Length..],
                SectionKey = entry.Key,
                IsEnabled = ReadBool(section[EnableKey]) ?? true,
                IsBlocked = ReadBool(section[BlockedKey]) ?? false,
            });
        }

        drivers.Sort(static (a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
        return drivers;
    }

    public void ApplyDrivers(IEnumerable<DriverInfo> drivers)
    {
        foreach (var driver in drivers)
        {
            var section = EnsureSection(driver.SectionKey);

            section[EnableKey] = driver.IsEnabled;

            // Never write blocked state; unblocking simply removes the key so SteamVR owns it again.
            if (!driver.IsBlocked)
                section.Remove(BlockedKey);
        }
    }

    public string Save()
    {
        var backup = FileStorage.Backup(FilePath);
        FileStorage.WriteAllTextAtomic(FilePath, Serialize());
        return backup ?? string.Empty;
    }

    private JsonObject EnsureSection(string sectionKey)
    {
        if (_root[sectionKey] is JsonObject section)
            return section;

        section = new JsonObject();
        _root[sectionKey] = section;
        return section;
    }

    private string Serialize() => _root.ToJsonString(new JsonSerializerOptions { WriteIndented = true });

    private static bool? ReadBool(JsonNode? node)
    {
        if (node is not JsonValue value)
            return null;

        try
        {
            if (value.TryGetValue<bool>(out var boolValue))
                return boolValue;

            if (value.TryGetValue<string>(out var stringValue) && bool.TryParse(stringValue, out var parsed))
                return parsed;
        }
        catch
        {
            // Ignore malformed values.
        }

        return null;
    }
}
