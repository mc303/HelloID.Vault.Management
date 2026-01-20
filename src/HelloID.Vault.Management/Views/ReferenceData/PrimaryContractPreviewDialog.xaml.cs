using System.Windows;
using HelloID.Vault.Management.ViewModels.ReferenceData;

namespace HelloID.Vault.Management.Views.ReferenceData;

/// <summary>
/// Interaction logic for PrimaryContractPreviewDialog.xaml
/// </summary>
public partial class PrimaryContractPreviewDialog : Window
{
    public PrimaryContractPreviewDialog()
    {
        InitializeComponent();
    }

    public void SetViewModel(PrimaryContractPreviewViewModel viewModel)
    {
        DataContext = viewModel;
        viewModel.CloseRequested += OnCloseRequested;
    }

    private void OnCloseRequested()
    {
        Close();
    }

    protected override void OnClosed(EventArgs e)
    {
        if (DataContext is PrimaryContractPreviewViewModel viewModel)
        {
            viewModel.CloseRequested -= OnCloseRequested;
        }
        base.OnClosed(e);
    }
}
