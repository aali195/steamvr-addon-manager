using System.ComponentModel;
using System.Diagnostics;
using SteamVrAddonManager.Models;
using SteamVrAddonManager.Services;
using SteamVrAddonManager.ViewModels;

namespace SteamVrAddonManager;

/// <summary>
/// Main window. UI construction lives in <c>MainForm.Layout.cs</c>; this file holds the
/// control fields and all behaviour (event handling and model/UI synchronisation).
/// </summary>
public sealed partial class MainForm : Form
{
    private const string SteamVrRunningWarning =
        "SteamVR is running - changes may be overwritten when it exits.";

    private const string BlockedStatusText = "Blocked by safe mode";
    private const string OkStatusText = "OK";

    private readonly MainViewModel _vm;
    private readonly StartupAppsViewModel _startupVm;
    private readonly CheckedListBinder<DriverInfo> _driverBinder;
    private readonly CheckedListBinder<StartupApp> _appBinder;

    private readonly TextBox _pathBox = new()
    {
        ReadOnly = true,
        Dock = DockStyle.Fill,
        PlaceholderText = "No Steam config folder selected - click Browse...",
        Margin = new Padding(3, 4, 8, 4),
    };

    private readonly Label _warnLabel = new()
    {
        Text = SteamVrRunningWarning,
        AutoSize = true,
        Visible = false,
        ForeColor = Color.FromArgb(180, 95, 0),
        Margin = new Padding(4, 0, 4, 8),
    };

    private readonly Label _readOnlyLabel = new()
    {
        AutoSize = true,
        Visible = false,
        ForeColor = Color.FromArgb(200, 60, 0),
        Margin = new Padding(4, 2, 4, 2),
        Text = "This config folder is not writable - use Options \u2192 Restart as administrator to save changes.",
    };

    private readonly ListBox _presetList = new()
    {
        Dock = DockStyle.Fill,
        IntegralHeight = false,
        BorderStyle = BorderStyle.FixedSingle,
    };

    private readonly ListView _driverList = new()
    {
        Dock = DockStyle.Fill,
        View = View.Details,
        CheckBoxes = true,
        FullRowSelect = true,
        GridLines = false,
        HideSelection = false,
    };

    private readonly ListView _appList = new()
    {
        Dock = DockStyle.Fill,
        View = View.Details,
        CheckBoxes = true,
        FullRowSelect = true,
        GridLines = false,
        HideSelection = false,
    };

    private readonly TextBox _newPresetBox = new() { Width = 240, Margin = new Padding(3, 4, 8, 4) };

    private readonly Label _blockedWarningLabel = new()
    {
        AutoSize = true,
        Visible = false,
        ForeColor = Color.FromArgb(200, 60, 0),
        Margin = new Padding(0, 6, 8, 0),
    };

    private readonly Button _unblockAllButton = new()
    {
        Text = "Unblock All Blocked",
        AutoSize = true,
        Visible = false,
        Margin = new Padding(0, 0, 8, 0),
    };

    private readonly Label _noBlockedLabel = new()
    {
        Text = "No add-ons are blocked.",
        AutoSize = true,
        Visible = false,
        ForeColor = Color.FromArgb(0, 128, 0),
        Margin = new Padding(4, 6, 0, 0),
    };

    private readonly Label _appCountLabel = new()
    {
        AutoSize = true,
        ForeColor = Color.DimGray,
        Margin = new Padding(4, 6, 0, 0),
    };

    private readonly ContextMenuStrip _appMenu = new();
    private readonly ToolStripMenuItem _removeOrphanItem = new("Remove orphan record (immediate)");

    private readonly MenuStrip _menu = new();
    private readonly ToolStripMenuItem _taskbarMenuItem = new("Show presets in the taskbar menu");

    private readonly Label _dirtyLabel = new()
    {
        Text = "Unsaved changes",
        AutoSize = true,
        Visible = false,
        ForeColor = Color.OrangeRed,
        Margin = new Padding(12, 8, 0, 0),
    };

    private readonly Label _statusLabel = new()
    {
        AutoSize = true,
        ForeColor = Color.DimGray,
        Margin = new Padding(4, 8, 4, 0),
        TextAlign = ContentAlignment.MiddleLeft,
    };

    // Mutable UI state.
    private bool _configWritable = true;
    private StartupApp? _contextApp;
    private bool _syncingMenu;
    private bool _syncingPreset;
    private bool _syncingNewPreset;

    public MainForm(MainViewModel viewModel, StartupAppsViewModel startupViewModel)
    {
        _vm = viewModel;
        _startupVm = startupViewModel;

        _driverBinder = new CheckedListBinder<DriverInfo>(
            _driverList,
            static driver => driver.IsEnabled,
            static (driver, value) => driver.IsEnabled = value,
            static driver => driver.Name,
            StatusText);

        _appBinder = new CheckedListBinder<StartupApp>(
            _appList,
            static app => app.StartWithSteamVr,
            static (app, value) => app.StartWithSteamVr = value,
            static app => app.Name,
            AppDetail);

        BuildLayout();
        WireEvents();

        _vm.Initialize();
        _startupVm.Load(_vm.ConfigDirectory);

        RefreshDriverList();
        RefreshAppList();
        RebuildPresetList();

        _syncingMenu = true;
        _taskbarMenuItem.Checked = _vm.TaskbarJumpListEnabled;
        _syncingMenu = false;

        if (_vm.TaskbarJumpListEnabled)
            TaskbarJumpList.Refresh(_vm.Presets);

        UpdateConfigAccess();
        ShowLoadedStatus();
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);

        // The ListView raises spurious ItemChecked events while its native handle is created; let the
        // binders re-assert the model state first, then accept edits once the message queue settles.
        BeginInvoke((Action)(() =>
        {
            _driverBinder.MarkReady();
            _appBinder.MarkReady();
        }));
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        UpdateStatusWrapWidth();
    }

    // Caps the status line to the window width so long messages wrap instead of forcing the window wider.
    private void UpdateStatusWrapWidth() =>
        _statusLabel.MaximumSize = new Size(Math.Max(200, ClientSize.Width - 40), 0);

    private void WireEvents()
    {
        _vm.PropertyChanged += OnDriverViewModelChanged;
        _vm.Presets.CollectionChanged += OnPresetsChanged;

        _startupVm.PropertyChanged += OnStartupViewModelChanged;
        _startupVm.Apps.CollectionChanged += (_, _) => RefreshAppList();

        _presetList.SelectedIndexChanged += OnPresetSelectionChanged;
        _newPresetBox.TextChanged += OnNewPresetNameChanged;
        _unblockAllButton.Click += (_, _) => _vm.UnblockAllCommand.Execute(null);

        _appMenu.Items.Add(_removeOrphanItem);
        _removeOrphanItem.Click += OnRemoveOrphan;
        _appMenu.Opening += OnAppMenuOpening;
        _appList.ContextMenuStrip = _appMenu;
        _appList.MouseDown += OnAppListMouseDown;

        _taskbarMenuItem.CheckedChanged += OnTaskbarOptionChanged;
    }

    private void OnPresetsChanged(object? sender, EventArgs e)
    {
        RebuildPresetList();

        if (_vm.TaskbarJumpListEnabled)
            TaskbarJumpList.Refresh(_vm.Presets);
    }

    private void OnTaskbarOptionChanged(object? sender, EventArgs e)
    {
        if (_syncingMenu)
            return;

        var enabled = _taskbarMenuItem.Checked;
        _vm.SetTaskbarJumpListEnabled(enabled);

        if (enabled)
            TaskbarJumpList.Refresh(_vm.Presets);
        else
            TaskbarJumpList.Clear();
    }

    private void OnAppListMouseDown(object? sender, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Right)
            return;

        var hit = _appList.HitTest(e.Location);
        _contextApp = hit.Item?.Tag as StartupApp;

        if (hit.Item is not null)
            hit.Item.Selected = true;
    }

    private void OnAppMenuOpening(object? sender, CancelEventArgs e)
    {
        // The menu only appears on orphan rows - this deletes a file immediately.
        e.Cancel = _contextApp is not { IsOrphan: true };
    }

    private void OnRemoveOrphan(object? sender, EventArgs e)
    {
        if (_contextApp is not null)
            _startupVm.RemoveOrphan(_contextApp);
    }

    private void OnPresetSelectionChanged(object? sender, EventArgs e)
    {
        if (!_syncingPreset)
            _vm.SelectedPreset = _presetList.SelectedItem?.ToString();
    }

    private void OnNewPresetNameChanged(object? sender, EventArgs e)
    {
        if (!_syncingNewPreset)
            _vm.NewPresetName = _newPresetBox.Text;
    }

    private void OnBrowse(object? sender, EventArgs e)
    {
        using var dialog = new FolderBrowserDialog
        {
            Description = "Select the SteamVR config folder (the one containing steamvr.vrsettings)",
            UseDescriptionForTitle = true,
            ShowNewFolderButton = false,
        };

        if (!string.IsNullOrWhiteSpace(_vm.ConfigDirectory) && Directory.Exists(_vm.ConfigDirectory))
            dialog.SelectedPath = _vm.ConfigDirectory;

        if (dialog.ShowDialog(this) != DialogResult.OK)
            return;

        _vm.SetConfigDirectory(dialog.SelectedPath);
        ReloadAll();
    }

    private void OnOpenFolder(object? sender, EventArgs e)
    {
        var directory = _vm.ConfigDirectory;
        if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
        {
            MessageBox.Show(this, "No config folder is selected.", "Open Folder",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo(directory) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"Could not open the folder: {ex.Message}", "Open Folder",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void OnSave(object? sender, EventArgs e)
    {
        if (!_configWritable && PromptElevateForSave())
            return;

        var driverStatus = _vm.SaveDrivers();
        var appStatus = _startupVm.SaveApps();

        SetStatus(string.Join("  ", new[] { driverStatus, appStatus }.Where(static s => !string.IsNullOrWhiteSpace(s))));
        UpdateDirty();
        UpdateConfigAccess();
    }

    private void OnDeletePreset(object? sender, EventArgs e)
    {
        if (_vm.SelectedPreset is null)
            return;

        var confirm = MessageBox.Show(this, $"Delete preset '{_vm.SelectedPreset}'?", "Delete Preset",
            MessageBoxButtons.YesNo, MessageBoxIcon.Warning);

        if (confirm == DialogResult.Yes)
            _vm.DeletePresetCommand.Execute(null);
    }

    private void OnRenamePreset(object? sender, EventArgs e)
    {
        if (_vm.SelectedPreset is null)
            return;

        var newName = TextPrompt.Show(this, "Rename Preset", "New preset name:", _vm.SelectedPreset);
        if (string.IsNullOrWhiteSpace(newName) || newName == _vm.SelectedPreset)
            return;

        _vm.RenamePreset(newName);
    }

    private void OnDuplicatePreset(object? sender, EventArgs e) => _vm.DuplicatePreset();

    private void OnAbout(object? sender, EventArgs e) =>
        MessageBox.Show(this,
            $"SteamVR Addon Manager {Application.ProductVersion}\n\nManage SteamVR drivers and startup overlays.\nMIT licensed.",
            "About", MessageBoxButtons.OK, MessageBoxIcon.Information);

    private void OnRestartAsAdmin(object? sender, EventArgs e) => RestartElevated();

    private void UpdateConfigAccess()
    {
        var directory = _vm.ConfigDirectory;
        var exists = !string.IsNullOrWhiteSpace(directory) && Directory.Exists(directory);

        _configWritable = !exists || FileStorage.IsDirectoryWritable(directory!);
        _readOnlyLabel.Visible = exists && !_configWritable;
    }

    private bool PromptElevateForSave()
    {
        var choice = MessageBox.Show(this,
            "The Steam config folder is not writable.\n\nRestart as administrator to save changes?",
            "Not writable", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);

        if (choice != DialogResult.Yes)
            return false;

        RestartElevated();
        return true;
    }

    private void RestartElevated()
    {
        var exe = Environment.ProcessPath;
        if (string.IsNullOrEmpty(exe))
            return;

        try
        {
            Process.Start(new ProcessStartInfo(exe) { UseShellExecute = true, Verb = "runas" });
            Close();
        }
        catch (Win32Exception)
        {
            // User cancelled the elevation prompt.
        }
    }

    private void OnDriverViewModelChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(MainViewModel.ConfigDirectory):
                _pathBox.Text = _vm.ConfigDirectory ?? string.Empty;
                UpdateConfigAccess();
                break;

            case nameof(MainViewModel.StatusMessage):
                SetStatus(_vm.StatusMessage);
                break;

            case nameof(MainViewModel.IsDirty):
                UpdateDirty();
                break;

            case nameof(MainViewModel.SteamVrRunning):
                _warnLabel.Visible = _vm.SteamVrRunning;
                break;

            case nameof(MainViewModel.BlockedCount):
                UpdateBlockedIndicators();
                break;

            case nameof(MainViewModel.SelectedPreset):
                SyncPresetSelection();
                break;

            case nameof(MainViewModel.NewPresetName):
                SyncNewPresetName();
                break;
        }
    }

    private void OnStartupViewModelChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(StartupAppsViewModel.StatusMessage):
                SetStatus(_startupVm.StatusMessage);
                break;

            case nameof(StartupAppsViewModel.IsDirty):
                UpdateDirty();
                break;

            case nameof(StartupAppsViewModel.StartupCount):
                UpdateAppIndicators();
                break;
        }
    }

    private void ReloadAll()
    {
        _vm.ReloadCommand.Execute(null);
        _startupVm.Load(_vm.ConfigDirectory);
        RefreshDriverList();
        RefreshAppList();
        UpdateConfigAccess();
        ShowLoadedStatus();
    }

    private void ShowLoadedStatus() =>
        SetStatus($"Loaded {_vm.Drivers.Count} driver(s) and {_startupVm.Apps.Count} startup overlay(s).");

    private void RefreshDriverList()
    {
        _driverBinder.Rebuild(_vm.Drivers);
        UpdateBlockedIndicators();
    }

    private void RefreshAppList()
    {
        _appBinder.Rebuild(_startupVm.Apps);
        UpdateAppIndicators();
    }

    private void SetStatus(string message) => _statusLabel.Text = message;

    private void UpdateDirty() => _dirtyLabel.Visible = _vm.IsDirty || _startupVm.IsDirty;

    private void UpdateBlockedIndicators()
    {
        var hasBlocked = _vm.BlockedCount > 0;

        _blockedWarningLabel.Visible = hasBlocked;
        _blockedWarningLabel.Text = $"{_vm.BlockedCount} add-on(s) blocked by safe mode:";
        _unblockAllButton.Visible = hasBlocked;

        // Only reassure the user once a settings file with drivers is actually loaded.
        _noBlockedLabel.Visible = !hasBlocked && _vm.Drivers.Count > 0;
    }

    private void UpdateAppIndicators()
    {
        if (_startupVm.Apps.Count == 0)
        {
            _appCountLabel.Text = "No startup overlays found.";
            _appCountLabel.ForeColor = Color.DimGray;
            return;
        }

        _appCountLabel.Text = $"{_startupVm.StartupCount} overlay(s) start with SteamVR.";
        _appCountLabel.ForeColor = _startupVm.StartupCount > 0 ? Color.FromArgb(0, 128, 0) : Color.DimGray;
    }

    private void RebuildPresetList()
    {
        _syncingPreset = true;
        _presetList.BeginUpdate();
        _presetList.Items.Clear();
        foreach (var preset in _vm.Presets)
            _presetList.Items.Add(preset);
        _presetList.EndUpdate();
        _syncingPreset = false;

        SyncPresetSelection();
    }

    private void SyncPresetSelection()
    {
        _syncingPreset = true;

        if (_vm.SelectedPreset is not null && _presetList.Items.Contains(_vm.SelectedPreset))
            _presetList.SelectedItem = _vm.SelectedPreset;
        else if (_vm.SelectedPreset is null)
            _presetList.ClearSelected();

        _syncingPreset = false;
    }

    private void SyncNewPresetName()
    {
        if (_newPresetBox.Text == _vm.NewPresetName)
            return;

        _syncingNewPreset = true;
        _newPresetBox.Text = _vm.NewPresetName ?? string.Empty;
        _syncingNewPreset = false;
    }

    private static string StatusText(DriverInfo driver) =>
        driver.IsBlocked ? BlockedStatusText : OkStatusText;

    private static string AppDetail(StartupApp app) =>
        app.IsOrphan ? "orphan record" : app.LaunchType;
}
