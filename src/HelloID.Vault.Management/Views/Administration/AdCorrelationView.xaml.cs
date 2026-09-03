using System.Windows;
using System.Windows.Controls;
using HelloID.Vault.Management.ViewModels.Administration;

namespace HelloID.Vault.Management.Views.Administration;

/// <summary>
/// Interaction logic for AdCorrelationView.xaml.
/// </summary>
public partial class AdCorrelationView : UserControl
{
    private AdCorrelationViewModel? _viewModel;

    public AdCorrelationView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (_viewModel == null && DataContext is AdCorrelationViewModel viewModel)
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
