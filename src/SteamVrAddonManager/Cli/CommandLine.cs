namespace SteamVrAddonManager.Cli;

internal enum CliAction
{
    Gui,
    Apply,
}

/// <summary>The action requested on the command line.</summary>
internal sealed record CliCommand
{
    public CliAction Action { get; init; } = CliAction.Gui;

    public string? PresetName { get; init; }

    public string? ConfigDirectory { get; init; }

    public string? LogPath { get; init; }

    /// <summary>Set when the arguments were malformed; the caller should exit with an error.</summary>
    public string? Error { get; init; }
}

/// <summary>
/// Parses process arguments. Absent an <c>--apply</c> flag the app launches its GUI; otherwise it
/// runs the requested preset headlessly.
/// </summary>
internal static class CommandLine
{
    public static CliCommand Parse(string[] args)
    {
        string? preset = null;
        string? config = null;
        string? log = null;
        var hasApply = false;

        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--apply":
                    if (++i >= args.Length)
                        return Error("Missing value for --apply.");
                    hasApply = true;
                    preset = args[i];
                    break;

                case "--config":
                    if (++i >= args.Length)
                        return Error("Missing value for --config.");
                    config = args[i];
                    break;

                case "--log":
                    if (++i >= args.Length)
                        return Error("Missing value for --log.");
                    log = args[i];
                    break;
            }
        }

        if (!hasApply)
            return new CliCommand { Action = CliAction.Gui };

        if (string.IsNullOrWhiteSpace(preset))
            return Error("No preset name supplied.");

        return new CliCommand
        {
            Action = CliAction.Apply,
            PresetName = Clean(preset),
            ConfigDirectory = CleanOptional(config),
            LogPath = CleanOptional(log),
        };
    }

    private static CliCommand Error(string message) =>
        new() { Action = CliAction.Apply, Error = message };

    private static string Clean(string value) => value.Trim().Trim('"');

    private static string? CleanOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : Clean(value);
}
