using System.Data;
using System.Globalization;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using HelloID.Vault.Management.Controls;
using HelloID.Vault.Services.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Win32;

namespace HelloID.Vault.Management.Services;

/// <summary>
/// Exports DataGrid contents to a CSV file.
/// Exports visible columns and all rows currently shown in the grid,
/// so active filters and the column picker are respected.
/// The delimiter comes from the saved user preference; on first export
/// the user is asked and the choice can be remembered.
/// </summary>
public static class DataGridExportService
{
    /// <summary>
    /// Shows a save dialog and writes the grid contents to the chosen CSV file.
    /// </summary>
    /// <param name="grid">The DataGrid to export.</param>
    /// <param name="suggestedFileName">Default file name without extension.</param>
    public static void ExportToCsv(DataGrid grid, string suggestedFileName)
    {
        ArgumentNullException.ThrowIfNull(grid);
        ArgumentException.ThrowIfNullOrWhiteSpace(suggestedFileName);

        var dialog = new SaveFileDialog
        {
            FileName = suggestedFileName,
            Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*",
        };
        if (dialog.ShowDialog() != true)
        {
            return;
        }

        var delimiter = ResolveDelimiter(grid);

        var rows = grid.ItemsSource switch
        {
            null => Enumerable.Empty<object>(),
            // Use the default view so sorting/filtering applied to the collection is respected
            _ => CollectionViewSource.GetDefaultView(grid.ItemsSource).Cast<object>(),
        };

        var columns = grid.Columns
            .Where(c => c.Visibility == Visibility.Visible)
            .Select(c => (Column: c, Path: GetBindingPath(c)))
            .Where(x => x.Path != null)
            .ToList();

        var csv = new StringBuilder();
        csv.AppendLine(string.Join(delimiter, columns.Select(x => Escape(x.Column.Header?.ToString() ?? string.Empty, delimiter))));

        foreach (var item in rows)
        {
            csv.AppendLine(string.Join(delimiter, columns.Select(x => Escape(GetCellValue(item, x.Path!), delimiter))));
        }

        File.WriteAllText(dialog.FileName, csv.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
        System.Diagnostics.Debug.WriteLine($"[DataGridExport] Exported to {dialog.FileName}");
    }

    /// <summary>
    /// Returns the saved delimiter if the user chose to remember it;
    /// otherwise asks via a dialog and optionally saves the choice.
    /// Falls back to the system list separator when the dialog is cancelled.
    /// </summary>
    private static char ResolveDelimiter(DataGrid grid)
    {
        var preferences = (App.Current as App)?.Services.GetService<IUserPreferencesService>();

        var saved = preferences?.ExportDelimiter;
        if (!string.IsNullOrEmpty(saved))
        {
            return ParseDelimiter(saved);
        }

        var owner = Window.GetWindow(grid);
        var dialog = new DelimiterSelectionWindow();
        if (owner != null)
        {
            dialog.Owner = owner;
        }

        if (dialog.ShowDialog() == true)
        {
            if (dialog.RememberChoice && preferences != null)
            {
                preferences.ExportDelimiter = dialog.SelectedDelimiter.ToString();
            }

            return dialog.SelectedDelimiter;
        }

        return ParseDelimiter(CultureInfo.CurrentCulture.TextInfo.ListSeparator);
    }

    private static char ParseDelimiter(string value) =>
        value == "\\t" || value == "\t" ? '\t' : value.FirstOrDefault(',');

    private static string? GetBindingPath(DataGridColumn column) => column switch
    {
        DataGridTextColumn text => BindingPath(text.Binding),
        DataGridCheckBoxColumn check => BindingPath(check.Binding),
        DataGridComboBoxColumn combo => BindingPath(combo.SelectedItemBinding) ?? BindingPath(combo.TextBinding),
        DataGridTemplateColumn template => TemplateBindingPath(template),
        _ => null,
    };

    private static string? BindingPath(BindingBase? binding) =>
        binding is Binding b && !string.IsNullOrEmpty(b.Path.Path) ? b.Path.Path : null;

    private static string? TemplateBindingPath(DataGridTemplateColumn template)
    {
        // Instantiate the template outside the visual tree and read the first
        // plain TextBlock.Text binding (e.g. Score %); action buttons yield no
        // binding and are skipped.
        if (template.CellTemplate.LoadContent() is TextBlock textBlock &&
            textBlock.GetBindingExpression(TextBlock.TextProperty)?.ParentBinding.Path.Path is { Length: > 0 } path)
        {
            return path;
        }

        return null;
    }

    private static string GetCellValue(object item, string path) => item switch
    {
        DataRowView row => row[path]?.ToString() ?? string.Empty,
        _ => item.GetType().GetProperty(path)?.GetValue(item)?.ToString() ?? string.Empty,
    };

    private static string Escape(string value, char delimiter)
    {
        if (value.Contains(delimiter) || value.Contains('"') || value.Contains('\n') || value.Contains('\r'))
        {
            return $"\"{value.Replace("\"", "\"\"")}\"";
        }

        return value;
    }
}
