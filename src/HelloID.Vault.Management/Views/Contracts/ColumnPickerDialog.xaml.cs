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
            System.Diagnostics.Debug.WriteLine("[ColumnPickerDialog] Reset column order to default");
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
