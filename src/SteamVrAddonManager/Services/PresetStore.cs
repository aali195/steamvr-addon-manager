using System.Text.Json;
using SteamVrAddonManager.Models;

namespace SteamVrAddonManager.Services;

/// <summary>Stores driver presets as individual JSON files in the portable <c>presets</c> folder.</summary>
public sealed class PresetStore
{
    public IReadOnlyList<string> ListPresets()
    {
        try
        {
            if (!Directory.Exists(PortablePaths.PresetsDirectory))
                return Array.Empty<string>();

            return Directory.EnumerateFiles(PortablePaths.PresetsDirectory, "*.json")
                .Select(Path.GetFileNameWithoutExtension)
                .Where(static name => !string.IsNullOrWhiteSpace(name))
                .Select(static name => name!)
                .OrderBy(static name => name, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
        catch
        {
            return Array.Empty<string>();
        }
    }

    public Preset? Load(string name)
    {
        try
        {
            var file = GetFilePath(name);
            if (!File.Exists(file))
                return null;

            return JsonSerializer.Deserialize(File.ReadAllText(file), AppJsonContext.Default.Preset);
        }
        catch
        {
            return null;
        }
    }

    public void Save(Preset preset)
    {
        Directory.CreateDirectory(PortablePaths.PresetsDirectory);
        var json = JsonSerializer.Serialize(preset, AppJsonContext.Default.Preset);
        FileStorage.WriteAllTextAtomic(GetFilePath(preset.Name), json);
    }

    public void Delete(string name)
    {
        try
        {
            var file = GetFilePath(name);
            if (File.Exists(file))
                File.Delete(file);
        }
        catch
        {
            // Ignore: a preset that can't be deleted isn't worth interrupting the user for.
        }
    }

    /// <summary>Renames a preset, preserving its contents. Returns <c>false</c> when it can't be done.</summary>
    public bool Rename(string oldName, string newName)
    {
        if (string.IsNullOrWhiteSpace(newName))
            return false;

        var preset = Load(oldName);
        if (preset is null)
            return false;

        var oldPath = GetFilePath(oldName);
        var newPath = GetFilePath(newName);

        preset.Name = newName;
        Save(preset);

        if (!string.Equals(oldPath, newPath, StringComparison.OrdinalIgnoreCase))
            Delete(oldName);

        return true;
    }

    /// <summary>Copies a preset under a unique name and returns the new name, or <c>null</c> on failure.</summary>
    public string? Duplicate(string name)
    {
        var preset = Load(name);
        if (preset is null)
            return null;

        var newName = MakeUniqueName(name);
        preset.Name = newName;
        Save(preset);
        return newName;
    }

    private static string MakeUniqueName(string baseName)
    {
        var candidate = $"{baseName} copy";
        var index = 2;

        while (File.Exists(GetFilePath(candidate)))
            candidate = $"{baseName} copy {index++}";

        return candidate;
    }

    private static string GetFilePath(string name) =>
        Path.Combine(PortablePaths.PresetsDirectory, SanitizeName(name) + ".json");

    private static string SanitizeName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var cleaned = new string(name.Select(ch => invalid.Contains(ch) ? '_' : ch).ToArray()).Trim();
        return string.IsNullOrWhiteSpace(cleaned) ? "preset" : cleaned;
    }
}
