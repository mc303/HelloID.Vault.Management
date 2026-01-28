using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using HelloID.Vault.Management.ViewModels.Contracts;

namespace HelloID.Vault.Management.Views.Contracts;

/// <summary>
/// Dialog for selecting which DataGrid columns to display.
/// </summary>
public partial class ColumnPickerDialog : Window
{
    public DataGrid? TargetDataGrid { get; set; }

    public ColumnPickerDialog()
    {
        InitializeComponent();
        Debug.WriteLine("[ColumnPickerDialog] Constructor called");
        this.Loaded += (s, e) => Debug.WriteLine($"[ColumnPickerDialog] Loaded event - DataContext type: {DataContext?.GetType().Name}");
    }

    private async void OnSelectAllClick(object sender, RoutedEventArgs e)
    {
        Debug.WriteLine("[ColumnPickerDialog] OnSelectAllClick called");
        if (DataContext is ContractsViewModel viewModel)
        {
            Debug.WriteLine($"[ColumnPickerDialog] Got ContractsViewModel, calling ShowAllColumnsAsync");
            await viewModel.ShowAllColumnsAsync();
        }
        else
        {
            Debug.WriteLine($"[ColumnPickerDialog] ERROR: DataContext is {DataContext?.GetType().Name}, not ContractsViewModel");
        }
    }

    private void OnHideEmptyColumnsClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is ContractsViewModel viewModel)
        {
            viewModel.HideEmptyColumnsCommand.Execute(null);
        }
    }

    private void OnOkClick(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }

    private void OnResetColumnOrderClick(object sender, RoutedEventArgs e)
    {
        // Reset the DataGrid columns to default (XAML) order
        if (TargetDataGrid != null)
        {
            for (int i = 0; i < TargetDataGrid.Columns.Count; i++)
            {
                TargetDataGrid.Columns[i].DisplayIndex = i;
            }
        }

        // Clear the saved preference
        if (DataContext is ContractsViewModel viewModel)
        {
            viewModel.ResetColumnOrder();
        }

        // Close the dialog
        DialogResult = true;
        Close();
    }
}
