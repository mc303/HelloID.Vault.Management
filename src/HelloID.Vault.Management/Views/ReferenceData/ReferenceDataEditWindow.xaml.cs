using System.Windows;
using HelloID.Vault.Management.ViewModels.ReferenceData;

namespace HelloID.Vault.Management.Views.ReferenceData;

public partial class ReferenceDataEditWindow : Window
{
    public ReferenceDataEditWindow()
    {
        InitializeComponent();
    }

    public void SetViewModel(ReferenceDataEditViewModel viewModel)
    {
        DataContext = viewModel;
        viewModel.CloseRequested += OnCloseRequested;
    }

    private void OnCloseRequested(bool success)
    {
        DialogResult = success;
        Close();
    }

    protected override void OnClosed(EventArgs e)
    {
        if (DataContext is ReferenceDataEditViewModel viewModel)
        {
            viewModel.CloseRequested -= OnCloseRequested;
        }
        base.OnClosed(e);
    }
}
