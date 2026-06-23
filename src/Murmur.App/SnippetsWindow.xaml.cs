using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Murmur.Core.Text;

namespace Murmur.App;

/// <summary>
/// Editor for voice shortcuts (snippets). Edits a working copy; the saved list is exposed via
/// <see cref="Result"/> only when the user clicks Save.
/// </summary>
public partial class SnippetsWindow : Window
{
    private readonly ObservableCollection<Snippet> _items;

    public SnippetsWindow(IEnumerable<Snippet> current)
    {
        InitializeComponent();

        _items = new ObservableCollection<Snippet>(
            current.Select(s => new Snippet { Trigger = s.Trigger, Expansion = s.Expansion }));
        Grid.ItemsSource = _items;
    }

    /// <summary>The edited snippet list, set when the user saves.</summary>
    public List<Snippet>? Result { get; private set; }

    private void OnRemove(object sender, RoutedEventArgs e)
    {
        if (Grid.SelectedItem is Snippet snippet)
        {
            _items.Remove(snippet);
        }
    }

    private void OnSave(object sender, RoutedEventArgs e)
    {
        // Commit any in-progress grid edit, then keep only complete rows.
        Grid.CommitEdit(DataGridEditingUnit.Row, exitEditingMode: true);

        Result = _items
            .Where(s => !string.IsNullOrWhiteSpace(s.Trigger) && !string.IsNullOrWhiteSpace(s.Expansion))
            .Select(s => new Snippet { Trigger = s.Trigger.Trim(), Expansion = s.Expansion })
            .ToList();

        DialogResult = true;
    }
}
