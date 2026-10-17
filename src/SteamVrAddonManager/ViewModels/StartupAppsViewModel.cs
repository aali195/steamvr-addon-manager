using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SteamVrAddonManager.Models;
using SteamVrAddonManager.Services;

namespace SteamVrAddonManager.ViewModels;

/// <summary>Manages the "start with SteamVR" state of registered dashboard overlays.</summary>
public partial class StartupAppsViewModel : ObservableObject, IStartupAppState
{
    private readonly Dictionary<StartupApp, bool> _originalStates = new();

    private StartupAppCatalog? _catalog;

    public ObservableCollection<StartupApp> Apps { get; } = new();

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private bool _isDirty;

    [ObservableProperty]
    private int _startupCount;

    public void Load(string? configDirectory)
    {
        DetachApps();
        _catalog = null;

        var paths = SteamConfigPaths.FromConfigDirectory(configDirectory);
        if (paths is null)
        {
            StatusMessage = "No Steam config folder selected.";
            IsDirty = false;
            UpdateCount();
            return;
        }

        try
        {
            _catalog = new StartupAppCatalog(paths);
            foreach (var app in _catalog.Load())
            {
                app.PropertyChanged += OnAppChanged;
                Apps.Add(app);
                _originalStates[app] = app.StartWithSteamVr;
            }

            StatusMessage = $"Loaded {Apps.Count} startup overlay(s).";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to load startup overlays: {ex.Message}";
        }

        IsDirty = false;
        UpdateCount();
    }

    [RelayCommand]
    private void EnableAll() => SetAllStartup(true);

    [RelayCommand]
    private void DisableAll() => SetAllStartup(false);

    /// <summary>Writes changed overlays and returns a short status message.</summary>
    public string SaveApps()
    {
        if (_catalog is null)
            return string.Empty;

        var changed = Apps.Where(app => app.StartWithSteamVr != _originalStates[app]).ToList();
        if (changed.Count == 0)
        {
            IsDirty = false;
            return "No overlay changes to save.";
        }

        try
        {
            _catalog.Save(changed);
            foreach (var app in changed)
                _originalStates[app] = app.StartWithSteamVr;

            IsDirty = false;
            UpdateCount();
            StatusMessage = $"Saved {changed.Count} overlay change(s).";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to save startup overlays: {ex.Message}";
        }

        return StatusMessage;
    }

    /// <summary>Immediately backs up and deletes an orphan overlay's startup record.</summary>
    public string RemoveOrphan(StartupApp app)
    {
        if (_catalog is null || !app.IsOrphan)
            return string.Empty;

        try
        {
            _catalog.RemoveRecord(app.AppKey);
            app.PropertyChanged -= OnAppChanged;
            _originalStates.Remove(app);
            Apps.Remove(app);
            UpdateCount();
            StatusMessage = $"Removed orphan record '{app.AppKey}'.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to remove '{app.AppKey}': {ex.Message}";
        }

        return StatusMessage;
    }

    public IReadOnlyDictionary<string, bool> CaptureAutoLaunch() =>
        Apps.ToDictionary(static app => app.AppKey, static app => app.StartWithSteamVr, StringComparer.OrdinalIgnoreCase);

    public void ApplyAutoLaunch(IReadOnlyDictionary<string, bool> states)
    {
        foreach (var app in Apps)
        {
            if (states.TryGetValue(app.AppKey, out var enabled))
                app.StartWithSteamVr = enabled;
        }
    }

    private void OnAppChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(StartupApp.StartWithSteamVr))
            return;

        IsDirty = true;
        UpdateCount();
    }

    private void SetAllStartup(bool enabled)
    {
        foreach (var app in Apps)
            app.StartWithSteamVr = enabled;
    }

    private void UpdateCount() => StartupCount = Apps.Count(static app => app.StartWithSteamVr);

    private void DetachApps()
    {
        foreach (var app in Apps)
            app.PropertyChanged -= OnAppChanged;

        Apps.Clear();
        _originalStates.Clear();
    }
}
