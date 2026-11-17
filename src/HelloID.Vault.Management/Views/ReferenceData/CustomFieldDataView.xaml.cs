using System.Data;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using HelloID.Vault.Management.Services;
using HelloID.Vault.Core.Models.Entities;
using HelloID.Vault.Management.ViewModels.ReferenceData;

namespace HelloID.Vault.Management.Views.ReferenceData;

public partial class CustomFieldDataView : UserControl
{
    private CustomFieldDataViewModelBase? _viewModel;
    private bool _columnsCreated;

    public CustomFieldDataView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _viewModel = DataContext as CustomFieldDataViewModelBase;
        if (_viewModel == null) return;

        _viewModel.DataLoaded += OnDataLoaded;

        // If data already exists (returning to view), recreate columns and restore
        if (_viewModel.Data != null && _viewModel.Data.Rows.Count > 0)
        {
            CreateColumns(_viewModel.CurrentSchemas.ToList(), _viewModel.GetBaseColumns());
            _columnsCreated = true;

            if (_viewModel.SelectedRow != null && DataGrid.Items.Contains(_viewModel.SelectedRow))
            {
                DataGrid.ScrollIntoView(_viewModel.SelectedRow);
            }
        }
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (_viewModel != null)
        {
            _viewModel.DataLoaded -= OnDataLoaded;
            _viewModel = null;
        }
    }

    private void OnDataLoaded(DataTable? data, List<CustomFieldSchema> schemas)
    {
        if (!_columnsCreated && _viewModel != null)
        {
            CreateColumns(schemas, _viewModel.GetBaseColumns());
            _columnsCreated = true;
        }
    }

    private void CreateColumns(List<CustomFieldSchema> schemas, List<(string FieldName, string DisplayName, double Width)> baseColumns)
    {
        DataGrid.Columns.Clear();

        foreach (var (fieldName, displayName, width) in baseColumns)
        {
            DataGrid.Columns.Add(new DataGridTextColumn
            {
                Header = displayName,
                Binding = new Binding(fieldName),
                SortMemberPath = fieldName,
                Width = new DataGridLength(width)
            });
        }

        foreach (var schema in schemas)
        {
            DataGrid.Columns.Add(new DataGridTextColumn
            {
                Header = schema.DisplayName,
                Binding = new Binding(schema.FieldKey),
                SortMemberPath = schema.FieldKey,
                Width = new DataGridLength(150)
            });
        }

        Debug.WriteLine($"[CustomFieldDataView] Created {DataGrid.Columns.Count} columns for {_viewModel?.TableName}");
    }

    private void OnScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        if (_viewModel == null || _viewModel.IsLoading || !_viewModel.HasMoreData) return;

        var scrollViewer = e.OriginalSource as ScrollViewer;
        if (scrollViewer == null) return;

        if (scrollViewer.VerticalOffset >= scrollViewer.ScrollableHeight - 200)
        {
            _ = _viewModel.LoadMoreAsync();
        }
    }

    private void ExportToCsv_Click(object sender, RoutedEventArgs e)
    {
        // Context menu: grid is the placement target; toolbar button: find the first grid in the view
        DataGrid? dataGrid = sender is MenuItem menuItem && menuItem.Parent is ContextMenu contextMenu
            ? contextMenu.PlacementTarget as DataGrid
            : FindFirstChild<DataGrid>(this);

        if (dataGrid != null)
        {
            DataGridExportService.ExportToCsv(dataGrid, "custom_field_data");
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

