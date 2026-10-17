using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SteamVrAddonManager.Models;
using SteamVrAddonManager.Services;

namespace SteamVrAddonManager.ViewModels;

/// <summary>
/// State for the main window: the selected config folder, the driver list, preset management,
/// dirty tracking and SteamVR-running status.
/// </summary>
public partial class MainViewModel : ObservableObject
{
    private readonly AppSettingsStore _settingsStore;
    private readonly PresetStore _presetStore;
    private readonly IStartupAppState? _appState;
    private readonly AppSettings _settings;

    private VrSettingsDocument? _document;

    public MainViewModel(AppSettingsStore settingsStore, PresetStore presetStore, IStartupAppState? appState = null)
    {
        _settingsStore = settingsStore;
        _presetStore = presetStore;
        _appState = appState;
        _settings = settingsStore.Load();
    }

    public ObservableCollection<DriverInfo> Drivers { get; } = new();

    public ObservableCollection<string> Presets { get; } = new();

    [ObservableProperty]
    private string? _configDirectory;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private bool _isDirty;

    [ObservableProperty]
    private bool _steamVrRunning;

    [ObservableProperty]
    private int _blockedCount;

    [ObservableProperty]
    private string _newPresetName = string.Empty;

    [ObservableProperty]
    private string? _selectedPreset;

    [ObservableProperty]
    private bool _taskbarJumpListEnabled;

    public void Initialize()
    {
        ConfigDirectory = string.IsNullOrWhiteSpace(_settings.ConfigDirectory)
            ? SteamLocator.AutoDetectConfigDirectory()
            : _settings.ConfigDirectory;

        TaskbarJumpListEnabled = _settings.TaskbarJumpList;

        RefreshPresets();
        RefreshSteamVrStatus();
        Reload();
    }

    /// <summary>Remembers an explicitly chosen config folder so it is used on the next launch.</summary>
    public void SetConfigDirectory(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return;

        ConfigDirectory = path;
        _settings.ConfigDirectory = path;
        _settingsStore.Save(_settings);
    }

    /// <summary>Remembers whether presets are published to the taskbar Jump List.</summary>
    public void SetTaskbarJumpListEnabled(bool enabled)
    {
        if (TaskbarJumpListEnabled == enabled)
            return;

        TaskbarJumpListEnabled = enabled;
        _settings.TaskbarJumpList = enabled;
        _settingsStore.Save(_settings);
    }

    [RelayCommand]
    private void Reload()
    {
        _document = null;
        DetachDrivers();

        var directory = ConfigDirectory;
        try
        {
            if (string.IsNullOrWhiteSpace(directory))
            {
                StatusMessage = "No Steam config folder selected. Click Browse to locate it.";
                return;
            }

            if (!Directory.Exists(directory))
            {
                StatusMessage = $"Config folder not found: {directory}";
                return;
            }

            var vrSettings = new SteamConfigPaths(directory).VrSettingsFile;
            if (!File.Exists(vrSettings))
            {
                StatusMessage = $"No steamvr.vrsettings in {directory}. Startup overlays can still be managed.";
                return;
            }

            var document = VrSettingsDocument.Load(vrSettings);
            foreach (var driver in document.GetDrivers())
            {
                driver.PropertyChanged += OnDriverChanged;
                Drivers.Add(driver);
            }

            _document = document;
            StatusMessage = $"Loaded {Drivers.Count} driver(s) from {vrSettings}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to load settings: {ex.Message}";
        }
        finally
        {
            UpdateBlockedState();
            IsDirty = false;
        }
    }

    [RelayCommand]
    private void EnableAll() => SetAllDriversEnabled(true);

    [RelayCommand]
    private void DisableAll() => SetAllDriversEnabled(false);

    [RelayCommand]
    private void UnblockAll()
    {
        foreach (var driver in Drivers)
            driver.IsBlocked = false;
    }

    /// <summary>Writes driver enable states and returns a short status message.</summary>
    public string SaveDrivers()
    {
        if (_document is null)
        {
            StatusMessage = "Nothing to save - load a settings file first.";
            return StatusMessage;
        }

        try
        {
            _document.ApplyDrivers(Drivers);
            var backup = _document.Save();

            IsDirty = false;
            RefreshSteamVrStatus();

            StatusMessage = string.IsNullOrEmpty(backup)
                ? $"Saved drivers to {_document.FilePath}"
                : $"Saved drivers to {_document.FilePath} (backup: {Path.GetFileName(backup)})";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to save drivers: {ex.Message}";
        }

        return StatusMessage;
    }

    [RelayCommand]
    private void RefreshPresets()
    {
        var previouslySelected = SelectedPreset;

        Presets.Clear();
        foreach (var preset in _presetStore.ListPresets())
            Presets.Add(preset);

        SelectedPreset = previouslySelected is not null && Presets.Contains(previouslySelected)
            ? previouslySelected
            : Presets.FirstOrDefault();
    }

    [RelayCommand]
    private void SavePreset()
    {
        var name = NewPresetName.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            StatusMessage = "Enter a preset name first.";
            return;
        }

        var preset = new Preset { Name = name };
        foreach (var driver in Drivers)
            preset.Drivers[driver.Name] = new PresetDriverState { Enabled = driver.IsEnabled };

        if (_appState is not null)
        {
            foreach (var (appKey, autoLaunch) in _appState.CaptureAutoLaunch())
                preset.Apps[appKey] = new PresetAppState { AutoLaunch = autoLaunch };
        }

        _presetStore.Save(preset);
        NewPresetName = string.Empty;
        RefreshPresets();
        SelectedPreset = name;
        StatusMessage = $"Saved preset '{name}' ({Drivers.Count} driver(s)).";
    }

    [RelayCommand]
    private void ApplyPreset()
    {
        if (SelectedPreset is null)
        {
            StatusMessage = "Select a preset to apply.";
            return;
        }

        var preset = _presetStore.Load(SelectedPreset);
        if (preset is null)
        {
            StatusMessage = $"Could not load preset '{SelectedPreset}'.";
            return;
        }

        var applied = 0;
        foreach (var driver in Drivers)
        {
            if (!preset.Drivers.TryGetValue(driver.Name, out var state))
                continue;

            driver.IsEnabled = state.Enabled;
            applied++;
        }

        if (_appState is not null && preset.Apps.Count > 0)
        {
            _appState.ApplyAutoLaunch(preset.Apps.ToDictionary(
                static pair => pair.Key,
                static pair => pair.Value.AutoLaunch));
        }

        StatusMessage = $"Applied preset '{SelectedPreset}' to {applied} driver(s). Click Save to write.";
    }

    [RelayCommand]
    private void DeletePreset()
    {
        if (SelectedPreset is null)
        {
            StatusMessage = "Select a preset to delete.";
            return;
        }

        var name = SelectedPreset;
        _presetStore.Delete(name);
        RefreshPresets();
        StatusMessage = $"Deleted preset '{name}'.";
    }

    /// <summary>Renames the selected preset, keeping its contents.</summary>
    public bool RenamePreset(string newName)
    {
        if (SelectedPreset is null)
        {
            StatusMessage = "Select a preset to rename.";
            return false;
        }

        if (!_presetStore.Rename(SelectedPreset, newName))
        {
            StatusMessage = "Could not rename the preset.";
            return false;
        }

        RefreshPresets();
        SelectedPreset = newName;
        StatusMessage = $"Renamed preset to '{newName}'.";
        return true;
    }

    /// <summary>Duplicates the selected preset under a unique name.</summary>
    public void DuplicatePreset()
    {
        if (SelectedPreset is null)
        {
            StatusMessage = "Select a preset to duplicate.";
            return;
        }

        var newName = _presetStore.Duplicate(SelectedPreset);
        if (newName is null)
        {
            StatusMessage = "Could not duplicate the preset.";
            return;
        }

        RefreshPresets();
        SelectedPreset = newName;
        StatusMessage = $"Duplicated preset as '{newName}'.";
    }

    private void RefreshSteamVrStatus() => SteamVrRunning = SteamVrProcessCheck.IsRunning;

    private void SetAllDriversEnabled(bool enabled)
    {
        foreach (var driver in Drivers)
            driver.IsEnabled = enabled;
    }

    private void OnDriverChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(DriverInfo.IsEnabled):
                IsDirty = true;
                break;

            case nameof(DriverInfo.IsBlocked):
                IsDirty = true;
                UpdateBlockedState();
                break;
        }
    }

    private void UpdateBlockedState() => BlockedCount = Drivers.Count(static driver => driver.IsBlocked);

    private void DetachDrivers()
    {
        foreach (var driver in Drivers)
            driver.PropertyChanged -= OnDriverChanged;

        Drivers.Clear();
    }
}
