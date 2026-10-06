using System.Windows;
using System.Windows.Controls;
using HelloID.Vault.Management.ViewModels.Administration;

namespace HelloID.Vault.Management.Views.Administration;

/// <summary>
/// Interaction logic for AdSettingsView.xaml.
/// </summary>
public partial class AdSettingsView : UserControl
{
    private AdSettingsViewModel? _viewModel;

    public AdSettingsView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (_viewModel == null && DataContext is AdSettingsViewModel viewModel)
        {
            _viewModel = viewModel;
            _ = viewModel.InitializeAsync();
        }
    }

    private void PasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
    {
        if (_viewModel != null && sender is PasswordBox passwordBox)
        {
            _viewModel.Password = passwordBox.Password;
        }
    }
}
