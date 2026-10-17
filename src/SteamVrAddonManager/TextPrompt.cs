namespace SteamVrAddonManager;

/// <summary>Minimal single-line text input dialog.</summary>
internal sealed class TextPrompt : Form
{
    private readonly TextBox _input;

    private TextPrompt(string title, string prompt, string initialValue)
    {
        Text = title;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MinimizeBox = false;
        MaximizeBox = false;
        ShowInTaskbar = false;
        ClientSize = new Size(380, 112);
        Font = new Font("Segoe UI", 9F);

        var label = new Label { Text = prompt, AutoSize = true, Location = new Point(12, 12) };
        _input = new TextBox { Text = initialValue, Location = new Point(15, 38), Width = 350 };

        var ok = new Button { Text = "OK", DialogResult = DialogResult.OK, Location = new Point(200, 72), Width = 75 };
        var cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, Location = new Point(285, 72), Width = 75 };

        Controls.Add(label);
        Controls.Add(_input);
        Controls.Add(ok);
        Controls.Add(cancel);
        AcceptButton = ok;
        CancelButton = cancel;
    }

    /// <summary>Shows the prompt and returns the trimmed text, or <c>null</c> if cancelled.</summary>
    public static string? Show(IWin32Window owner, string title, string prompt, string initialValue)
    {
        using var dialog = new TextPrompt(title, prompt, initialValue);
        dialog._input.SelectAll();
        return dialog.ShowDialog(owner) == DialogResult.OK ? dialog._input.Text.Trim() : null;
    }
}
