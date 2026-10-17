using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace SteamVrAddonManager;

/// <summary>
/// Binds a checkbox <see cref="ListView"/> to a set of observable items, keeping each row's check
/// state and second ("detail") column in sync with the model. It also works around WinForms raising
/// spurious <c>ItemChecked</c> events while the native handle is created.
/// </summary>
internal sealed class CheckedListBinder<T> where T : ObservableObject
{
    private readonly ListView _list;
    private readonly Func<T, bool> _isChecked;
    private readonly Action<T, bool> _setChecked;
    private readonly Func<T, string> _name;
    private readonly Func<T, string> _detail;
    private readonly Dictionary<T, ListViewItem> _rows = new();

    private bool _syncing;
    private bool _ready;

    public CheckedListBinder(
        ListView list,
        Func<T, bool> isChecked,
        Action<T, bool> setChecked,
        Func<T, string> name,
        Func<T, string> detail)
    {
        _list = list;
        _isChecked = isChecked;
        _setChecked = setChecked;
        _name = name;
        _detail = detail;
        _list.ItemChecked += OnItemChecked;
    }

    /// <summary>Replaces the rows with <paramref name="source"/>, wiring up change tracking.</summary>
    public void Rebuild(IEnumerable<T> source)
    {
        foreach (var item in _rows.Keys)
            item.PropertyChanged -= OnItemPropertyChanged;

        _rows.Clear();

        _syncing = true;
        _list.BeginUpdate();
        _list.Items.Clear();

        foreach (var item in source)
        {
            var row = new ListViewItem(_name(item)) { Checked = _isChecked(item), Tag = item };
            row.SubItems.Add(_detail(item));
            _list.Items.Add(row);
            _rows[item] = row;
            item.PropertyChanged += OnItemPropertyChanged;
        }

        _list.EndUpdate();
        _syncing = false;
    }

    /// <summary>
    /// Re-asserts the check states from the model and then accepts user edits. Call this once the
    /// control is shown, after WinForms has finished creating the native handle.
    /// </summary>
    public void MarkReady()
    {
        _syncing = true;
        foreach (var (item, row) in _rows)
            row.Checked = _isChecked(item);
        _syncing = false;

        _ready = true;
    }

    private void OnItemChecked(object? sender, ItemCheckedEventArgs e)
    {
        if (_syncing || !_ready || e.Item.Tag is not T item)
            return;

        _setChecked(item, e.Item.Checked);
    }

    private void OnItemPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is not T item || !_rows.TryGetValue(item, out var row))
            return;

        _syncing = true;
        row.Checked = _isChecked(item);
        row.SubItems[1].Text = _detail(item);
        _syncing = false;
    }
}
