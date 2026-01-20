using System.Windows;
using HelloID.Vault.Management.ViewModels.Persons;

namespace HelloID.Vault.Management.Views.Persons;

/// <summary>
/// Interaction logic for ContactEditWindow.xaml
/// </summary>
public partial class ContactEditWindow : Window
{
    private ContactEditViewModel? _viewModel;

    public ContactEditWindow()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Sets the ViewModel for this window.
    /// </summary>
    /// <param name="viewModel">The ContactEditViewModel instance</param>
    public async void SetViewModel(ContactEditViewModel viewModel)
    {
        _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        DataContext = _viewModel;

        // DEBUG: Log DataContext setup
        System.Diagnostics.Debug.WriteLine($"ContactEditWindow.SetViewModel called - DataContext set to ContactEditViewModel");
        System.Diagnostics.Debug.WriteLine($"ViewModel state - IsLoading: {_viewModel.IsLoading}, HasError: {!string.IsNullOrEmpty(_viewModel.ErrorMessage)}");

        // Initialize async (loads existing contact types for Add mode)
        await _viewModel.InitializeAsync();
    }
}