using System.ComponentModel;
using System.Windows.Controls;
using System.Windows;
using System.Windows.Input;
using HelloID.Vault.Management.ViewModels.Contracts;
using HelloID.Vault.Core.Models;
using HelloID.Vault.Core.Utilities;
using HelloID.Vault.Management.Helpers;
using Microsoft.Extensions.DependencyInjection;
using System.Windows.Data;

namespace HelloID.Vault.Management.Views.Contracts;

public partial class ContractsView : UserControl
{
    private ContractsViewModel? _viewModel;

    // Track visibility binding to properly subscribe/unsubscribe
    private ContractsColumnVisibility? _trackedColumnVisibility;

    private record ColumnInfo(string Header, double Width, string SortPath);

    public ContractsView()
    {
        InitializeComponent();

        // Add PreviewCopy command handler to intercept before DataGrid's built-in handler
        CommandManager.AddPreviewExecutedHandler(ContractsDataGrid, OnCopyPreviewExecuted);
    }

    private void OnCopyPreviewExecuted(object sender, ExecutedRoutedEventArgs e)
    {
        // Only handle the Copy command
        if (e.Command != ApplicationCommands.Copy)
            return;

        // Copy the current cell's content to clipboard
        if (ContractsDataGrid.CurrentCell.IsValid && ContractsDataGrid.CurrentCell.Column != null)
        {
            var cellValue = ContractsDataGrid.CurrentCell.Item?.GetType()
                .GetProperty(ContractsDataGrid.CurrentCell.Column.SortMemberPath)?
                .GetValue(ContractsDataGrid.CurrentCell.Item);

            if (cellValue != null)
            {
                Clipboard.SetText(cellValue.ToString() ?? "");
                e.Handled = true; // Prevent default DataGrid row copy behavior
            }
        }
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        // Clear columns before loading data to defer column creation
        ContractsDataGrid.Columns.Clear();

        // Initialize view model references (data already loaded by navigation command)
        if (DataContext is ContractsViewModel viewModel)
        {
            _viewModel = viewModel; // Store reference for later use
            viewModel.ContractsDataGrid = ContractsDataGrid; // Set DataGrid reference for column picker
            viewModel.DataLoaded += OnDataLoaded; // Subscribe to data loaded event

            // If data is already loaded (InitializeAsync finished before View.Loaded), create columns now
            if (_viewModel.TotalCount > 0 || _viewModel.Contracts.Count > 0)
            {
                OnDataLoaded(this, EventArgs.Empty);
            }
        }
    }

    /// <summary>
    /// Creates DataGrid columns dynamically after data is loaded.
    /// Creates ALL columns (visible and hidden) and sets initial visibility.
    /// This is required so hidden columns can be shown later via ColumnPicker.
    /// </summary>
    private void OnDataLoaded(object? sender, EventArgs e)
    {
        if (_viewModel == null)
        {
            return;
        }

        try
        {
            // Get column visibility preferences
            var columnVisibility = _viewModel.ColumnVisibility;

            // Subscribe to property changes for real-time column visibility updates
            // This replaces the BindingProxy approach which doesn't work reliably with Freezable
            if (_trackedColumnVisibility != null)
            {
                _trackedColumnVisibility.PropertyChanged -= OnColumnVisibilityPropertyChanged;
            }
            _trackedColumnVisibility = columnVisibility;
            _trackedColumnVisibility.PropertyChanged += OnColumnVisibilityPropertyChanged;

            // Create ALL columns - both visible and hidden
            // This is necessary so hidden columns can be shown later via ColumnPicker
            CreateAllColumns(columnVisibility);

            // Apply saved column order and widths after columns are created
            ApplyColumnOrder();
            ApplyColumnWidths();
        }
        catch (Exception)
        {
            // Log exception if needed
        }
    }

    /// <summary>
    /// Handles property changes from ColumnVisibility to directly update DataGridColumn visibility.
    /// This bypasses the unreliable BindingProxy + Freezable binding mechanism.
    /// </summary>
    private void OnColumnVisibilityPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (string.IsNullOrEmpty(e.PropertyName) || _viewModel == null)
            return;

        // Find which column(s) use this property and update visibility
        var columnVisibility = _viewModel.ColumnVisibility;

        // Map property name to column header using centralized constants
        DataGridConstants.Contracts.VisibilityPropertyToDisplayName.TryGetValue(e.PropertyName, out var columnHeader);

        if (columnHeader == null)
        {
            return;
        }

        // Find the column with matching header
        var column = ContractsDataGrid.Columns.FirstOrDefault(c =>
            c.Header != null && c.Header.ToString() == columnHeader);

        if (column != null)
        {
            // Get visibility value directly from the property using reflection
            var property = columnVisibility.GetType().GetProperty(e.PropertyName);
            bool isVisible = (bool)(property?.GetValue(columnVisibility) ?? true);

            column.Visibility = isVisible ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    /// <summary>
    /// Creates ALL DataGrid columns (visible and hidden) and sets initial visibility.
    /// This is necessary so hidden columns can be shown later via ColumnPicker.
    /// </summary>
    private void CreateAllColumns(ContractsColumnVisibility columnVisibility)
    {
        // Helper to get column info from centralized constants
        ColumnInfo? GetColumnInfo(string property)
        {
            if (DataGridConstants.Contracts.DefaultColumnWidths.TryGetValue(property, out var width))
            {
                var displayName = DataGridConstants.Contracts.AllColumns
                    .FirstOrDefault(c => c.PropertyName == property).DisplayName;
                return new ColumnInfo(displayName, width, property);
            }
            return null;
        }

        // Get DateTimeFormatConverter from resources
        var dateTimeConverter = Application.Current?.FindResource("DateTimeFormatConverter") as IValueConverter;

        // Get ALL column definitions (not just visible ones)
        var allProperties = ContractsColumnVisibility.AllColumns.Select(c => c.ColumnName).ToList();

        // Create ALL columns - this allows hidden columns to be shown later via ColumnPicker
        foreach (var property in allProperties)
        {
            var info = GetColumnInfo(property);
            if (info == null) continue;

            // Get initial visibility from preferences - use display name (Header) for lookup
            bool isVisible = columnVisibility.GetColumnVisibility(info.Header);

            var binding = new Binding(property);
            // Apply DateTimeFormatConverter to date columns using centralized constants
            if (DataGridConstants.Contracts.DateProperties.Contains(property) && dateTimeConverter != null)
            {
                binding.Converter = dateTimeConverter;
            }

            var column = new DataGridTextColumn
            {
                Header = info.Header,
                Binding = binding,
                SortMemberPath = property,
                Width = new DataGridLength(info.Width, DataGridLengthUnitType.Pixel),
                Visibility = isVisible ? Visibility.Visible : Visibility.Collapsed
            };

            ContractsDataGrid.Columns.Add(column);
        }
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        // Save column order and widths when leaving view
        SaveColumnOrder();
        SaveColumnWidths();

        // Unsubscribe from DataLoaded event to prevent memory leaks
        if (_viewModel != null)
        {
            _viewModel.DataLoaded -= OnDataLoaded;
        }

        // Unsubscribe from property changes
        if (_trackedColumnVisibility != null)
        {
            _trackedColumnVisibility.PropertyChanged -= OnColumnVisibilityPropertyChanged;
        }
        _trackedColumnVisibility = null;

        // Clear stored reference
        _viewModel = null;
    }

    private void ApplyColumnOrder()
    {
        if (_viewModel == null)
        {
            return;
        }

        // Get saved order from ViewModel
        var savedOrder = _viewModel.GetSavedColumnOrder();
        if (savedOrder == null || savedOrder.Count == 0)
        {
            return;
        }

        // Apply saved display indices to columns
        for (int i = 0; i < savedOrder.Count && i < ContractsDataGrid.Columns.Count; i++)
        {
            var columnName = savedOrder[i];
            var column = ContractsDataGrid.Columns.FirstOrDefault(c =>
            {
                // Try to get the column header or binding path to identify the column
                if (c.Header != null && c.Header.ToString() == columnName)
                    return true;

                // Also check by SortMemberPath (property name)
                return c.SortMemberPath == columnName;
            });

            if (column != null)
            {
                column.DisplayIndex = i;
            }
        }
    }

    /// <summary>
    /// Saves the current column order to preferences.
    /// </summary>
    private void SaveColumnOrder()
    {
        if (_viewModel == null)
        {
            return;
        }

        // Get current column order from DataGrid
        var columnOrder = ContractsDataGrid.Columns
            .OrderBy(c => c.DisplayIndex)
            .Select(c => c.SortMemberPath)
            .ToList();

        // Only save if we have all columns (DataGrid might not be fully loaded yet)
        if (columnOrder.Count > 0)
        {
            _viewModel.SaveColumnOrder(columnOrder);
        }
    }

    /// <summary>
    /// Applies the saved column widths to the DataGrid.
    /// </summary>
    private void ApplyColumnWidths()
    {
        if (_viewModel == null)
        {
            return;
        }

        var savedWidths = _viewModel.GetSavedColumnWidths();
        if (savedWidths == null || savedWidths.Count == 0)
        {
            return;
        }

        foreach (var kvp in savedWidths)
        {
            var column = ContractsDataGrid.Columns.FirstOrDefault(c =>
                c.SortMemberPath == kvp.Key || (c.Header != null && c.Header.ToString() == kvp.Key));

            if (column != null)
            {
                column.Width = new DataGridLength(kvp.Value, DataGridLengthUnitType.Pixel);
            }
        }
    }

    /// <summary>
    /// Saves the current column widths to preferences.
    /// </summary>
    private void SaveColumnWidths()
    {
        if (_viewModel == null)
        {
            return;
        }

        var columnWidths = new Dictionary<string, double>();
        foreach (var column in ContractsDataGrid.Columns)
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
}
