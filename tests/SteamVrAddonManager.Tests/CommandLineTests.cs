using SteamVrAddonManager.Cli;

namespace SteamVrAddonManager.Tests;

public sealed class CommandLineTests
{
    [Fact]
    public void NoArguments_LaunchesGui()
    {
        Assert.Equal(CliAction.Gui, CommandLine.Parse(Array.Empty<string>()).Action);
    }

    [Fact]
    public void Apply_ParsesPresetName()
    {
        var command = CommandLine.Parse(new[] { "--apply", "Sim racing" });

        Assert.Equal(CliAction.Apply, command.Action);
        Assert.Equal("Sim racing", command.PresetName);
        Assert.Null(command.Error);
    }

    [Fact]
    public void Apply_WithConfigAndLog_ParsesAll()
    {
        var command = CommandLine.Parse(new[] { "--apply", "X", "--config", @"C:\steam\config", "--log", "log.txt" });

        Assert.Equal("X", command.PresetName);
        Assert.Equal(@"C:\steam\config", command.ConfigDirectory);
        Assert.Equal("log.txt", command.LogPath);
    }

    [Fact]
    public void Apply_WithoutValue_IsAnError()
    {
        Assert.NotNull(CommandLine.Parse(new[] { "--apply" }).Error);
    }

    [Fact]
    public void ConfigWithoutApply_StillLaunchesGui()
    {
        Assert.Equal(CliAction.Gui, CommandLine.Parse(new[] { "--config", @"C:\steam\config" }).Action);
    }
}
