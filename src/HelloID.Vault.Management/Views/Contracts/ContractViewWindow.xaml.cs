using System.Windows;
using HelloID.Vault.Management.ViewModels.Contracts;

namespace HelloID.Vault.Management.Views.Contracts;

public partial class ContractViewWindow : Window
{
    public ContractViewWindow()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Sets the ViewModel and displays the contract JSON
    /// </summary>
    /// <param name="viewModel">The view model containing the contract data</param>
    public void SetViewModel(ContractViewViewModel viewModel)
    {
        DataContext = viewModel;

        // Set the JSON content to the TextBox
        if (viewModel != null)
        {
            JsonContentTextBox.Text = viewModel.JsonContent;
        }
    }

    /// <summary>
    /// Closes the window when the Close button is clicked
    /// </summary>
    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}