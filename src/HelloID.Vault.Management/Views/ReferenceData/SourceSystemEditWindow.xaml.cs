using System.Windows;
using HelloID.Vault.Management.ViewModels.ReferenceData;

namespace HelloID.Vault.Management.Views.ReferenceData;

/// <summary>
/// Interaction logic for SourceSystemEditWindow.xaml
/// </summary>
public partial class SourceSystemEditWindow : Window
{
    private readonly SourceSystemEditViewModel _viewModel;

    public SourceSystemEditWindow(SourceSystemEditViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        DataContext = _viewModel;

        _viewModel.CloseRequested += (bool result) =>
        {
            DialogResult = result;
            Close();
        };
    }
}
