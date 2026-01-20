using System.Windows.Controls;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Diagnostics;
using HelloID.Vault.Management.ViewModels.Contracts;
using HelloID.Vault.Core.Models;
using HelloID.Vault.Management.Helpers;
using Microsoft.Extensions.DependencyInjection;
using System.Windows.Data;

namespace HelloID.Vault.Management.Views.Contracts;

public partial class ContractsView : UserControl
{
    private ContractsViewModel? _viewModel;
    private static readonly Dictionary<string, string> ColumnToPropertyMap = new(StringComparer.OrdinalIgnoreCase)
    {
        { "ContractId", nameof(ContractsColumnVisibility.ShowContractId) },
        { "ExternalId", nameof(ContractsColumnVisibility.ShowExternalId) },
        { "PersonId", nameof(ContractsColumnVisibility.ShowPersonId) },
        { "PersonName", nameof(ContractsColumnVisibility.ShowPersonName) },
        { "StartDate", nameof(ContractsColumnVisibility.ShowStartDate) },
        { "EndDate", nameof(ContractsColumnVisibility.ShowEndDate) },
        { "TypeCode", nameof(ContractsColumnVisibility.ShowTypeCode) },
        { "TypeDescription", nameof(ContractsColumnVisibility.ShowTypeDescription) },
        { "Fte", nameof(ContractsColumnVisibility.ShowFte) },
        { "HoursPerWeek", nameof(ContractsColumnVisibility.ShowHoursPerWeek) },
        { "Percentage", nameof(ContractsColumnVisibility.ShowPercentage) },
        { "Sequence", nameof(ContractsColumnVisibility.ShowSequence) },
        { "ContractStatus", nameof(ContractsColumnVisibility.ShowContractStatus) },
        { "Source", nameof(ContractsColumnVisibility.ShowSource) },
        { "ManagerPersonId", nameof(ContractsColumnVisibility.ShowManagerPersonId) },
        { "ManagerPersonName", nameof(ContractsColumnVisibility.ShowManagerPersonName) },
        { "LocationCode", nameof(ContractsColumnVisibility.ShowLocationId) },
        { "LocationName", nameof(ContractsColumnVisibility.ShowLocationName) },
        { "CostCenterCode", nameof(ContractsColumnVisibility.ShowCostCenterId) },
        { "CostCenterName", nameof(ContractsColumnVisibility.ShowCostCenterName) },
        { "CostBearerCode", nameof(ContractsColumnVisibility.ShowCostBearerId) },
        { "CostBearerName", nameof(ContractsColumnVisibility.ShowCostBearerName) },
        { "EmployerCode", nameof(ContractsColumnVisibility.ShowEmployerId) },
        { "EmployerName", nameof(ContractsColumnVisibility.ShowEmployerName) },
        { "TeamCode", nameof(ContractsColumnVisibility.ShowTeamId) },
        { "TeamName", nameof(ContractsColumnVisibility.ShowTeamName) },
        { "DepartmentName", nameof(ContractsColumnVisibility.ShowDepartmentName) },
        { "DepartmentExternalId", nameof(ContractsColumnVisibility.ShowDepartmentId) },
        { "DepartmentCode", nameof(ContractsColumnVisibility.ShowDepartmentCode) },
        { "DepartmentManagerName", nameof(ContractsColumnVisibility.ShowDepartmentManagerName) },
        { "DepartmentParentDepartmentName", nameof(ContractsColumnVisibility.ShowDepartmentParentDepartmentName) },
        { "DivisionCode", nameof(ContractsColumnVisibility.ShowDivisionId) },
        { "DivisionName", nameof(ContractsColumnVisibility.ShowDivisionName) },
        { "TitleCode", nameof(ContractsColumnVisibility.ShowTitleId) },
        { "TitleName", nameof(ContractsColumnVisibility.ShowTitleName) },
        { "OrganizationCode", nameof(ContractsColumnVisibility.ShowOrganizationId) },
        { "OrganizationName", nameof(ContractsColumnVisibility.ShowOrganizationName) },
    };
    
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

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        System.Diagnostics.Debug.WriteLine("[ContractsView] ===== ContractsView_Loaded START =====");

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
                System.Diagnostics.Debug.WriteLine("[ContractsView] Data already loaded, creating columns directly");
                OnDataLoaded(this, EventArgs.Empty);
            }
        }

        stopwatch.Stop();
        System.Diagnostics.Debug.WriteLine($"[VIEW-LOAD] ContractsView OnLoaded END: {stopwatch.ElapsedMilliseconds}ms");
    }

    /// <summary>
    /// Creates DataGrid columns dynamically after data is loaded.
    /// Only creates columns that are visible in preferences.
    /// </summary>
    private void OnDataLoaded(object? sender, EventArgs e)
    {
        var createStopwatch = System.Diagnostics.Stopwatch.StartNew();
        System.Diagnostics.Debug.WriteLine("[ContractsView] ===== OnDataLoaded START =====");

        if (_viewModel == null)
        {
            System.Diagnostics.Debug.WriteLine("[ContractsView] OnDataLoaded - _viewModel is null, returning");
            return;
        }

        try
        {
            // Get column visibility preferences
            var columnVisibility = _viewModel.ColumnVisibility;

            System.Diagnostics.Debug.WriteLine("[ContractsView] Creating columns for visible properties only");
            var visibleColumns = ContractsColumnVisibility.AllColumns.Where(c => columnVisibility.GetColumnVisibility(c.ColumnName)).Select(c => c.ColumnName).ToList();
            System.Diagnostics.Debug.WriteLine($"[ContractsView] Visible columns: {string.Join(", ", visibleColumns)}");

            // Create columns dynamically based on property type
            CreateVisibleColumns(visibleColumns);

            System.Diagnostics.Debug.WriteLine($"[ContractsView] Column creation complete. Total DataGrid columns: {ContractsDataGrid.Columns.Count}");

            // Apply saved column order and widths after columns are created
            ApplyColumnOrder();
            ApplyColumnWidths();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ContractsView] Error creating columns: {ex.Message}");
        }
        finally
        {
            createStopwatch.Stop();
            System.Diagnostics.Debug.WriteLine($"[ContractsView] ===== OnDataLoaded END: {createStopwatch.ElapsedMilliseconds}ms =====");
        }
    }
    
    /// <summary>
    /// Creates visible DataGrid columns based on preferences.
    /// </summary>
    private void CreateVisibleColumns(List<string> visibleProperties)
    {
        // Helper to get column info from preferences
        ColumnInfo GetColumnInfo(string property)
        {
            return property switch
            {
                "ContractId" => new ColumnInfo("ID", 80, "ContractId"),
                "ExternalId" => new ColumnInfo("ExternalID", 120, "ExternalId"),
                "PersonId" => new ColumnInfo("Person ID", 150, "PersonId"),
                "PersonName" => new ColumnInfo("Person Name", 180, "PersonName"),
                "StartDate" => new ColumnInfo("Start Date", 100, "StartDate"),
                "EndDate" => new ColumnInfo("End Date", 100, "EndDate"),
                "TypeCode" => new ColumnInfo("Type Code", 90, "TypeCode"),
                "TypeDescription" => new ColumnInfo("Type Description", 150, "TypeDescription"),
                "Fte" => new ColumnInfo("FTE", 70, "Fte"),
                "HoursPerWeek" => new ColumnInfo("Hours/Week", 90, "HoursPerWeek"),
                "Percentage" => new ColumnInfo("Percentage", 90, "Percentage"),
                "Sequence" => new ColumnInfo("Sequence", 80, "Sequence"),
                "ContractStatus" => new ColumnInfo("Status", 90, "ContractStatus"),
                "Source" => new ColumnInfo("Source", 150, "Source"),
                "ManagerPersonId" => new ColumnInfo("Manager ExternalID", 150, "ManagerPersonId"),
                "ManagerPersonName" => new ColumnInfo("Manager Name", 180, "ManagerPersonName"),
                "LocationCode" => new ColumnInfo("Location Code", 120, "LocationCode"),
                "LocationName" => new ColumnInfo("Location Name", 180, "LocationName"),
                "CostCenterCode" => new ColumnInfo("Cost Center Code", 120, "CostCenterCode"),
                "CostCenterName" => new ColumnInfo("Cost Center Name", 180, "CostCenterName"),
                "CostBearerCode" => new ColumnInfo("Cost Bearer Code", 120, "CostBearerCode"),
                "CostBearerName" => new ColumnInfo("Cost Bearer Name", 180, "CostBearerName"),
                "EmployerCode" => new ColumnInfo("Employer Code", 120, "EmployerCode"),
                "EmployerName" => new ColumnInfo("Employer Name", 180, "EmployerName"),
                "TeamCode" => new ColumnInfo("Team Code", 120, "TeamCode"),
                "TeamName" => new ColumnInfo("Team Name", 180, "TeamName"),
                "DepartmentName" => new ColumnInfo("Department Name", 180, "DepartmentName"),
                "DepartmentExternalId" => new ColumnInfo("Department ExternalID", 150, "DepartmentExternalId"),
                "DepartmentCode" => new ColumnInfo("Department Code", 120, "DepartmentCode"),
                "DepartmentManagerName" => new ColumnInfo("Dept Manager", 180, "DepartmentManagerName"),
                "DepartmentParentDepartmentName" => new ColumnInfo("Parent Dept", 180, "DepartmentParentDepartmentName"),
                "DivisionCode" => new ColumnInfo("Division Code", 120, "DivisionCode"),
                "DivisionName" => new ColumnInfo("Division Name", 180, "DivisionName"),
                "TitleCode" => new ColumnInfo("Title Code", 120, "TitleCode"),
                "TitleName" => new ColumnInfo("Title Name", 180, "TitleName"),
                "OrganizationCode" => new ColumnInfo("Organization Code", 120, "OrganizationCode"),
                "OrganizationName" => new ColumnInfo("Organization Name", 180, "OrganizationName"),
                _ => null
            };
        }
        
        // Create columns for each visible property
        foreach (var property in visibleProperties)
        {
            var info = GetColumnInfo(property);
            if (info == null) continue;
            
            // Get resources for visibility binding
            var visibilityProxy = (BindingProxy)FindResource("ColumnVisibilityProxy");
            var visibilityConverter = (IValueConverter)FindResource("BooleanToVisibilityConverter");

            // Get visibility property name for this column
            var visibilityProperty = ColumnToPropertyMap[property];

            var column = new DataGridTextColumn
            {
                Header = info.Header,
                Binding = new Binding(property),
                SortMemberPath = property,
                Width = new DataGridLength(info.Width, DataGridLengthUnitType.Pixel)
            };
            
            // Set visibility binding separately using BindingOperations
            BindingOperations.SetBinding(column, DataGridColumn.VisibilityProperty, new Binding
            {
                Path = new PropertyPath($"Data.{visibilityProperty}"),
                Source = visibilityProxy,
                Converter = visibilityConverter
            });
            
            ContractsDataGrid.Columns.Add(column);
            System.Diagnostics.Debug.WriteLine($"[ContractsView] Created column: {info.Header} (property: {property})");
        }
    }
    
    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        // Save column order and widths when leaving view
        SaveColumnOrder();
        SaveColumnWidths();
        
        // Clear stored reference
        _viewModel = null;
    }

    private void ApplyColumnOrder()
    {
        System.Diagnostics.Debug.WriteLine("[ContractsView] ApplyColumnOrder() START");

        if (_viewModel == null)
        {
            System.Diagnostics.Debug.WriteLine("[ContractsView] ApplyColumnOrder() - _viewModel is null");
            return;
        }

        // Get saved order from ViewModel
        var savedOrder = _viewModel.GetSavedColumnOrder();
        if (savedOrder == null)
        {
            System.Diagnostics.Debug.WriteLine("[ContractsView] ApplyColumnOrder() - savedOrder is null");
            return;
        }

        if (savedOrder.Count == 0)
        {
            System.Diagnostics.Debug.WriteLine("[ContractsView] ApplyColumnOrder() - savedOrder is empty");
            return;
        }

        System.Diagnostics.Debug.WriteLine($"[ContractsView] ApplyColumnOrder() - savedOrder has {savedOrder.Count} columns: [{string.Join(", ", savedOrder)}]");

        // List all DataGrid columns for debugging
        System.Diagnostics.Debug.WriteLine($"[ContractsView] DataGrid has {ContractsDataGrid.Columns.Count} columns:");
        foreach (var c in ContractsDataGrid.Columns)
        {
            System.Diagnostics.Debug.WriteLine($"  - Header: '{c.Header}', SortMemberPath: '{c.SortMemberPath}', DisplayIndex: {c.DisplayIndex}");
        }

        // Apply saved display indices to columns
        int matchedCount = 0;
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
                var oldIndex = column.DisplayIndex;
                column.DisplayIndex = i;
                matchedCount++;
                System.Diagnostics.Debug.WriteLine($"[ContractsView] Set column '{columnName}' DisplayIndex from {oldIndex} to {i}");
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"[ContractsView] WARNING: Could not find column matching '{columnName}'");
            }
        }

        System.Diagnostics.Debug.WriteLine($"[ContractsView] Applied saved column order. Matched {matchedCount}/{savedOrder.Count} columns.");
    }

    /// <summary>
    /// Saves the current column order to preferences.
    /// </summary>
    private void SaveColumnOrder()
    {
        System.Diagnostics.Debug.WriteLine("[ContractsView] SaveColumnOrder() START");

        if (_viewModel == null)
        {
            System.Diagnostics.Debug.WriteLine("[ContractsView] SaveColumnOrder() - _viewModel is null");
            return;
        }

        // Get current column order from DataGrid
        var columnOrder = ContractsDataGrid.Columns
            .OrderBy(c => c.DisplayIndex)
            .Select(c => c.SortMemberPath)
            .ToList();

        System.Diagnostics.Debug.WriteLine($"[ContractsView] Current DataGrid column order ({columnOrder.Count} columns): [{string.Join(", ", columnOrder)}]");

        // Only save if we have all columns (DataGrid might not be fully loaded yet)
        if (columnOrder.Count > 0)
        {
            _viewModel.SaveColumnOrder(columnOrder);
            System.Diagnostics.Debug.WriteLine($"[ContractsView] Saved column order ({columnOrder.Count} columns).");
        }
        else
        {
            System.Diagnostics.Debug.WriteLine("[ContractsView] WARNING: columnOrder is empty, not saving");
        }
    }

    /// <summary>
    /// Applies the saved column widths to the DataGrid.
    /// </summary>
    private void ApplyColumnWidths()
    {
        System.Diagnostics.Debug.WriteLine("[ContractsView] ApplyColumnWidths() START");

        if (_viewModel == null)
        {
            System.Diagnostics.Debug.WriteLine("[ContractsView] ApplyColumnWidths() - _viewModel is null");
            return;
        }

        var savedWidths = _viewModel.GetSavedColumnWidths();
        if (savedWidths == null || savedWidths.Count == 0)
        {
            System.Diagnostics.Debug.WriteLine("[ContractsView] ApplyColumnWidths() - no saved widths");
            return;
        }

        foreach (var kvp in savedWidths)
        {
            var column = ContractsDataGrid.Columns.FirstOrDefault(c =>
                c.SortMemberPath == kvp.Key || (c.Header != null && c.Header.ToString() == kvp.Key));

            if (column != null)
            {
                column.Width = new DataGridLength(kvp.Value, DataGridLengthUnitType.Pixel);
                System.Diagnostics.Debug.WriteLine($"[ContractsView] Set column '{kvp.Key}' width to {kvp.Value}px");
            }
        }

        System.Diagnostics.Debug.WriteLine("[ContractsView] ApplyColumnWidths() END");
    }

    /// <summary>
    /// Saves the current column widths to preferences.
    /// </summary>
    private void SaveColumnWidths()
    {
        System.Diagnostics.Debug.WriteLine("[ContractsView] SaveColumnWidths() START");

        if (_viewModel == null)
        {
            System.Diagnostics.Debug.WriteLine("[ContractsView] SaveColumnWidths() - _viewModel is null");
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
            System.Diagnostics.Debug.WriteLine($"[ContractsView] Saved {columnWidths.Count} column widths");
        }

        System.Diagnostics.Debug.WriteLine("[ContractsView] SaveColumnWidths() END");
    }
}
