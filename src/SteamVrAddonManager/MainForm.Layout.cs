namespace SteamVrAddonManager;

/// <summary>UI construction for <see cref="MainForm"/>. Behaviour lives in <c>MainForm.cs</c>.</summary>
public sealed partial class MainForm
{
    private void BuildLayout()
    {
        Text = "SteamVR Addon Manager";
        ClientSize = new Size(1280, 1040);
        MinimumSize = new Size(1000, 760);
        StartPosition = FormStartPosition.CenterScreen;
        Font = new Font("Segoe UI", 9F);

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 6,
            Padding = new Padding(12),
        };
        // Fixed-width column so a long status/warning line can never push the layout (and window) wider.
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));     // settings file
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));     // SteamVR warning
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); // drivers + startup overlays
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));     // presets
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));     // create preset
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));     // status

        root.Controls.Add(BuildSettingsGroup(), 0, 0);
        root.Controls.Add(_warnLabel, 0, 1);
        root.Controls.Add(BuildColumns(), 0, 2);
        root.Controls.Add(BuildPresetsGroup(), 0, 3);
        root.Controls.Add(BuildNewPresetGroup(), 0, 4);
        root.Controls.Add(_statusLabel, 0, 5);

        Controls.Add(root);

        var menu = BuildMenu();
        Controls.Add(menu);
        MainMenuStrip = menu;

        UpdateStatusWrapWidth();
    }

    private MenuStrip BuildMenu()
    {
        _taskbarMenuItem.CheckOnClick = true;
        _taskbarMenuItem.ToolTipText =
            "Adds your presets to this app's taskbar right-click menu. Pin the app to the taskbar to use them.";

        var restartItem = new ToolStripMenuItem("Restart as administrator");
        restartItem.Click += OnRestartAsAdmin;

        var options = new ToolStripMenuItem("Options");
        options.DropDownItems.Add(_taskbarMenuItem);
        options.DropDownItems.Add(new ToolStripSeparator());
        options.DropDownItems.Add(restartItem);

        var aboutItem = new ToolStripMenuItem("About");
        aboutItem.Click += OnAbout;

        var help = new ToolStripMenuItem("Help");
        help.DropDownItems.Add(aboutItem);

        _menu.Items.Add(options);
        _menu.Items.Add(help);
        _menu.ShowItemToolTips = true;
        return _menu;
    }

    private GroupBox BuildSettingsGroup()
    {
        var browseButton = new Button { Text = "Browse...", AutoSize = true, Margin = new Padding(0, 3, 6, 3) };
        var reloadButton = new Button { Text = "Reload", AutoSize = true, Margin = new Padding(0, 3, 6, 3) };
        var openButton = new Button { Text = "Open Folder", AutoSize = true, Margin = new Padding(0, 3, 0, 3) };
        var saveButton = new Button { Text = "Save", AutoSize = true, Font = new Font(Font, FontStyle.Bold), Margin = new Padding(16, 3, 0, 3) };

        browseButton.Click += OnBrowse;
        reloadButton.Click += (_, _) => ReloadAll();
        openButton.Click += OnOpenFolder;
        saveButton.Click += OnSave;

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 6,
            RowCount = 2,
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.Controls.Add(_pathBox, 0, 0);
        layout.Controls.Add(browseButton, 1, 0);
        layout.Controls.Add(reloadButton, 2, 0);
        layout.Controls.Add(openButton, 3, 0);
        layout.Controls.Add(saveButton, 4, 0);
        layout.Controls.Add(_dirtyLabel, 5, 0);
        layout.Controls.Add(_readOnlyLabel, 0, 1);
        layout.SetColumnSpan(_readOnlyLabel, 6);

        return WrapGroup("SteamVR Config Folder", layout);
    }

    private TableLayoutPanel BuildColumns()
    {
        var columns = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
        };
        columns.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        columns.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        columns.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        columns.Controls.Add(BuildDriversGroup(), 0, 0);
        columns.Controls.Add(BuildStartupAppsGroup(), 1, 0);
        return columns;
    }

    private GroupBox BuildDriversGroup()
    {
        _driverList.Columns.Add("Driver", 340);
        _driverList.Columns.Add("Status", 200);

        var enableAll = new Button { Text = "Enable All", AutoSize = true, Margin = new Padding(0, 0, 8, 0) };
        var disableAll = new Button { Text = "Disable All", AutoSize = true, Margin = new Padding(0, 0, 8, 0) };
        enableAll.Click += (_, _) => _vm.EnableAllCommand.Execute(null);
        disableAll.Click += (_, _) => _vm.DisableAllCommand.Execute(null);

        var actions = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            WrapContents = true,
            Margin = new Padding(0, 0, 0, 6),
        };
        actions.Controls.Add(enableAll);
        actions.Controls.Add(disableAll);
        actions.Controls.Add(_blockedWarningLabel);
        actions.Controls.Add(_unblockAllButton);

        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3 };
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.Controls.Add(actions, 0, 0);
        layout.Controls.Add(_driverList, 0, 1);
        layout.Controls.Add(_noBlockedLabel, 0, 2);

        return WrapGroup("Drivers", layout, rightMargin: 6, autoSize: false);
    }

    private GroupBox BuildStartupAppsGroup()
    {
        _appList.Columns.Add("Overlay", 340);
        _appList.Columns.Add("Type", 160);

        var enableAll = new Button { Text = "Enable All", AutoSize = true, Margin = new Padding(0, 0, 8, 0) };
        var disableAll = new Button { Text = "Disable All", AutoSize = true, Margin = new Padding(0, 0, 8, 0) };
        enableAll.Click += (_, _) => _startupVm.EnableAllCommand.Execute(null);
        disableAll.Click += (_, _) => _startupVm.DisableAllCommand.Execute(null);

        var actions = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            WrapContents = true,
            Margin = new Padding(0, 0, 0, 6),
        };
        actions.Controls.Add(enableAll);
        actions.Controls.Add(disableAll);

        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3 };
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.Controls.Add(actions, 0, 0);
        layout.Controls.Add(_appList, 0, 1);
        layout.Controls.Add(_appCountLabel, 0, 2);

        return WrapGroup("Startup Overlays", layout, leftMargin: 6, autoSize: false);
    }

    private GroupBox BuildPresetsGroup()
    {
        _presetList.Height = 110;

        var applyButton = new Button { Text = "Apply", AutoSize = true, Margin = new Padding(0, 0, 0, 6) };
        var renameButton = new Button { Text = "Rename", AutoSize = true, Margin = new Padding(0, 0, 0, 6) };
        var duplicateButton = new Button { Text = "Duplicate", AutoSize = true, Margin = new Padding(0, 0, 0, 6) };
        var deleteButton = new Button { Text = "Delete", AutoSize = true, Margin = new Padding(0, 0, 0, 0) };
        var refreshButton = new Button { Text = "Refresh", AutoSize = true, Margin = new Padding(0, 12, 0, 0) };

        applyButton.Click += (_, _) => _vm.ApplyPresetCommand.Execute(null);
        renameButton.Click += OnRenamePreset;
        duplicateButton.Click += OnDuplicatePreset;
        deleteButton.Click += OnDeletePreset;
        refreshButton.Click += (_, _) => _vm.RefreshPresetsCommand.Execute(null);

        var actions = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Margin = new Padding(10, 0, 0, 0),
        };
        actions.Controls.Add(applyButton);
        actions.Controls.Add(renameButton);
        actions.Controls.Add(duplicateButton);
        actions.Controls.Add(deleteButton);
        actions.Controls.Add(refreshButton);

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 2,
            RowCount = 1,
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        layout.Controls.Add(_presetList, 0, 0);
        layout.Controls.Add(actions, 1, 0);

        return WrapGroup("Presets (drivers + startup overlays)", layout);
    }

    private GroupBox BuildNewPresetGroup()
    {
        var saveButton = new Button { Text = "Save Preset", AutoSize = true, Margin = new Padding(0, 3, 0, 3) };
        saveButton.Click += (_, _) => _vm.SavePresetCommand.Execute(null);

        var hint = new Label
        {
            Text = "Saves the current enabled state of every driver and the startup state of every overlay as a new preset.",
            AutoSize = true,
            ForeColor = Color.DimGray,
            Margin = new Padding(4, 2, 0, 0),
        };

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 3,
            RowCount = 2,
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var label = new Label { Text = "Preset name:", AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(4, 8, 0, 0) };
        layout.Controls.Add(label, 0, 0);
        layout.Controls.Add(_newPresetBox, 1, 0);
        layout.Controls.Add(saveButton, 2, 0);
        layout.Controls.Add(hint, 0, 1);
        layout.SetColumnSpan(hint, 3);

        return WrapGroup("Create New Preset", layout);
    }

    private static GroupBox WrapGroup(string title, Control content, int leftMargin = 0, int rightMargin = 0, bool autoSize = true) => new()
    {
        Text = title,
        Dock = DockStyle.Fill,
        AutoSize = autoSize,
        AutoSizeMode = AutoSizeMode.GrowAndShrink,
        Padding = new Padding(10),
        Margin = new Padding(leftMargin, 0, rightMargin, 10),
        Controls = { content },
    };
}
