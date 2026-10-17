using System.Text.Json;
using System.Text.Json.Serialization;
using SteamVrAddonManager.Models;

namespace SteamVrAddonManager.Services;

[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(AppSettings))]
[JsonSerializable(typeof(Preset))]
internal partial class AppJsonContext : JsonSerializerContext
{
}
