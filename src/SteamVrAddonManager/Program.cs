using SteamVrAddonManager.Cli;
using SteamVrAddonManager.Services;
using SteamVrAddonManager.ViewModels;

namespace SteamVrAddonManager;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        var command = CommandLine.Parse(args);

        if (command.Action == CliAction.Gui)
        {
            ApplicationConfiguration.Initialize();

            using var instance = SingleInstance.Acquire();
            if (instance is null)
                return PresetRunner.Success;

            var settingsStore = new AppSettingsStore();
            var presetStore = new PresetStore();
            var startupViewModel = new StartupAppsViewModel();
            var viewModel = new MainViewModel(settingsStore, presetStore, startupViewModel);

            var form = new MainForm(viewModel, startupViewModel);
            instance.Listen(form);

            Application.Run(form);
            return PresetRunner.Success;
        }

        if (command.Error is not null)
            return PresetRunner.InvalidArguments;

        return PresetRunner.Apply(command.PresetName!, command.ConfigDirectory, command.LogPath);
    }
}
