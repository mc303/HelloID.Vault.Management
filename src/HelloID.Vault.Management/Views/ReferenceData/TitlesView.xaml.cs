using System.Windows.Controls;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using HelloID.Vault.Management.ViewModels.ReferenceData;

namespace HelloID.Vault.Management.Views.ReferenceData;

public partial class TitlesView : UserControl
{
    private TitlesViewModel? _viewModel;

    public TitlesView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {

        var dataGrid = FindFirstChild<DataGrid>(this);
        if (dataGrid != null)
        {
            CommandManager.AddPreviewExecutedHandler(dataGrid, OnCopyPreviewExecuted);

            if (DataContext is TitlesViewModel viewModel)
            {
                _viewModel = viewModel;
                ApplyColumnOrderAndWidths(dataGrid);
            }
        }

    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {

        var dataGrid = FindFirstChild<DataGrid>(this);
        if (dataGrid != null && _viewModel != null)
        {
            SaveColumnOrderAndWidths(dataGrid);
        }

        _viewModel = null;

    }

    private void ApplyColumnOrderAndWidths(DataGrid dataGrid)
    {
        if (_viewModel == null) return;

        var savedOrder = _viewModel.GetSavedColumnOrder();
        if (savedOrder != null && savedOrder.Count > 0)
        {
            for (int i = 0; i < savedOrder.Count && i < dataGrid.Columns.Count; i++)
            {
                var columnName = savedOrder[i];
                var column = dataGrid.Columns.FirstOrDefault(c =>
                    c.SortMemberPath == columnName || (c.Header != null && c.Header.ToString() == columnName));

                if (column != null)
                {
                    column.DisplayIndex = i;
                }
            }
        }

        var savedWidths = _viewModel.GetSavedColumnWidths();
        if (savedWidths != null && savedWidths.Count > 0)
        {
            foreach (var kvp in savedWidths)
            {
                var column = dataGrid.Columns.FirstOrDefault(c =>
                    c.SortMemberPath == kvp.Key || (c.Header != null && c.Header.ToString() == kvp.Key));

                if (column != null)
                {
                    column.Width = new DataGridLength(kvp.Value, DataGridLengthUnitType.Pixel);
                }
            }
        }
    }

    private void SaveColumnOrderAndWidths(DataGrid dataGrid)
    {
        if (_viewModel == null) return;

        var columnOrder = dataGrid.Columns
            .OrderBy(c => c.DisplayIndex)
            .Select(c => c.SortMemberPath)
            .ToList();

        if (columnOrder.Count > 0)
        {
            _viewModel.SaveColumnOrder(columnOrder);
        }

        var columnWidths = new Dictionary<string, double>();
        foreach (var column in dataGrid.Columns)
        {
            var key = column.SortMemberPath;
            if (!string.IsNullOrEmpty(key))
            {
                columnWidths[key] = column.Width.DisplayValue;
            }
        }

        if (columnWidths.Count > 0)
        {
            _viewModel.SaveColumnWidths(columnWidths);
        }
    }

    private void OnCopyPreviewExecuted(object sender, ExecutedRoutedEventArgs e)
    {
        if (e.Command != ApplicationCommands.Copy)
            return;

        var dataGrid = FindFirstChild<DataGrid>(this);
        if (dataGrid != null)
        {
            CopyCurrentCell(dataGrid, e);
        }
    }

    private void CopyCurrentCell(DataGrid dataGrid, ExecutedRoutedEventArgs e)
    {
        if (dataGrid.CurrentCell.IsValid && dataGrid.CurrentCell.Column != null)
        {
            var cellValue = dataGrid.CurrentCell.Item?.GetType()
                .GetProperty(dataGrid.CurrentCell.Column.SortMemberPath)?
                .GetValue(dataGrid.CurrentCell.Item);

            if (cellValue != null)
            {
                Clipboard.SetText(cellValue.ToString() ?? "");
                e.Handled = true;
            }
        }
    }

    private static T? FindFirstChild<T>(DependencyObject parent) where T : DependencyObject
    {
        if (parent == null) return null;

        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T result)
                return result;

            var resultOfChild = FindFirstChild<T>(child);
            if (resultOfChild != null)
                return resultOfChild;
        }
        return null;
    }
}
