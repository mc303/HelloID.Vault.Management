using System.Windows;
using System.Windows.Controls;
using HelloID.Vault.Management.Services;
using HelloID.Vault.Management.ViewModels.Administration;

namespace HelloID.Vault.Management.Views.Administration;

/// <summary>
/// Interaction logic for AdRecommendationsView.xaml.
/// </summary>
public partial class AdRecommendationsView : UserControl
{
    private AdRecommendationsViewModel? _viewModel;

    public AdRecommendationsView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (_viewModel == null && DataContext is AdRecommendationsViewModel viewModel)
        {
            _viewModel = viewModel;
            _ = viewModel.InitializeAsync();
        }
    }

    private void ExportToCsv_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem menuItem && menuItem.Parent is ContextMenu contextMenu && contextMenu.PlacementTarget is DataGrid dataGrid)
        {
            DataGridExportService.ExportToCsv(dataGrid, "ad_recommendations");
        }
    }

}

