using System.Windows;
using System.Windows.Controls;
using HelloID.Vault.Management.ViewModels.Administration;

namespace HelloID.Vault.Management.Views.Administration;

/// <summary>
/// Interaction logic for AdResultsView.xaml.
/// </summary>
public partial class AdResultsView : UserControl
{
    private AdResultsViewModel? _viewModel;

    public AdResultsView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (_viewModel == null && DataContext is AdResultsViewModel viewModel)
        {
            _viewModel = viewModel;
            _ = viewModel.InitializeAsync();
        }
    }
}
