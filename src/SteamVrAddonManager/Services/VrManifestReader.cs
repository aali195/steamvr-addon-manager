using System.Text;
using System.Text.Json;

namespace SteamVrAddonManager.Services;

/// <summary>One application declared in a <c>.vrmanifest</c> file.</summary>
public sealed record ManifestApp(
    string AppKey,
    string Name,
    string LaunchType,
    bool IsDashboardOverlay);

/// <summary>Reads the <c>applications</c> array from a SteamVR <c>.vrmanifest</c> file.</summary>
public static class VrManifestReader
{
    private static readonly string[] PreferredLocales = { "en_us", "en-US" };

    public static IReadOnlyList<ManifestApp> Read(string manifestPath)
    {
        var json = File.ReadAllText(manifestPath, Encoding.UTF8);

        using var document = JsonDocument.Parse(json, new JsonDocumentOptions
        {
            CommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
        });

        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object ||
            !root.TryGetProperty("applications", out var applications) ||
            applications.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<ManifestApp>();
        }

        var result = new List<ManifestApp>();
        foreach (var app in applications.EnumerateArray())
        {
            if (app.ValueKind != JsonValueKind.Object)
                continue;

            var appKey = GetString(app, "app_key");
            if (string.IsNullOrWhiteSpace(appKey))
                continue;

            result.Add(new ManifestApp(
                appKey,
                LocalizedString(app, "name") ?? appKey,
                GetString(app, "launch_type") ?? string.Empty,
                IsTruthy(app, "is_dashboard_overlay")));
        }

        return result;
    }

    private static string? GetString(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    /// <summary>Accepts a real boolean, a number (1), or a string ("true"/"1") — manifests vary.</summary>
    private static bool IsTruthy(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var value))
            return false;

        return value.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.Number => value.TryGetDouble(out var number) && number != 0,
            JsonValueKind.String => value.GetString() is { } text &&
                                    (text.Equals("true", StringComparison.OrdinalIgnoreCase) || text == "1"),
            _ => false,
        };
    }

    private static string? LocalizedString(JsonElement app, string field)
    {
        if (!app.TryGetProperty("strings", out var strings) || strings.ValueKind != JsonValueKind.Object)
            return null;

        foreach (var locale in PreferredLocales)
        {
            if (strings.TryGetProperty(locale, out var section) &&
                section.ValueKind == JsonValueKind.Object &&
                section.TryGetProperty(field, out var value) &&
                value.ValueKind == JsonValueKind.String)
            {
                return value.GetString();
            }
        }

        // Fall back to any locale that has a non-empty value.
        foreach (var section in strings.EnumerateObject())
        {
            if (section.Value.ValueKind == JsonValueKind.Object &&
                section.Value.TryGetProperty(field, out var value) &&
                value.ValueKind == JsonValueKind.String &&
                !string.IsNullOrEmpty(value.GetString()))
            {
                return value.GetString();
            }
        }

        return null;
    }
}
