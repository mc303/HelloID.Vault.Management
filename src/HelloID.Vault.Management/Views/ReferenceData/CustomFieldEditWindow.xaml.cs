using System.Windows;
using HelloID.Vault.Management.ViewModels.ReferenceData;

namespace HelloID.Vault.Management.Views.ReferenceData;

/// <summary>
/// Interaction logic for CustomFieldEditWindow.xaml
/// </summary>
public partial class CustomFieldEditWindow : Window
{
    public CustomFieldEditWindow()
    {
        InitializeComponent();
    }

    public void SetViewModel(CustomFieldEditViewModel viewModel)
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
        if (DataContext is CustomFieldEditViewModel viewModel)
        {
            viewModel.CloseRequested -= OnCloseRequested;
        }
        base.OnClosed(e);
    }
}
