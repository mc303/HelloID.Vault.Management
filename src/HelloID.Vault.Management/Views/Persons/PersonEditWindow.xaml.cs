using System.Windows;
using HelloID.Vault.Management.ViewModels.Persons;

namespace HelloID.Vault.Management.Views.Persons;

/// <summary>
/// Interaction logic for PersonEditWindow.xaml
/// </summary>
public partial class PersonEditWindow : Window
{
    public PersonEditWindow()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Sets the DataContext and subscribes to window close requests from the ViewModel.
    /// </summary>
    public void SetViewModel(PersonEditViewModel viewModel)
    {
        System.Diagnostics.Debug.WriteLine($"[PersonEditWindow] SetViewModel called");
        DataContext = viewModel;
        viewModel.CloseRequested += (result) =>
        {
            DialogResult = result;
            Close();
        };
    }
}
