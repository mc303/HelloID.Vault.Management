using System.Windows;
using HelloID.Vault.Management.ViewModels.ReferenceData;

namespace HelloID.Vault.Management.Views.ReferenceData;

public partial class DepartmentEditWindow : Window
{
    public DepartmentEditWindow()
    {
        InitializeComponent();
    }

    public void SetViewModel(DepartmentEditViewModel viewModel)
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
        if (DataContext is DepartmentEditViewModel viewModel)
        {
            viewModel.CloseRequested -= OnCloseRequested;
        }
        base.OnClosed(e);
    }
}
