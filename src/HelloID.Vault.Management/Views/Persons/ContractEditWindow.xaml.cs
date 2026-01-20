using System.Windows;
using HelloID.Vault.Management.ViewModels.Persons;

namespace HelloID.Vault.Management.Views.Persons;

public partial class ContractEditWindow : Window
{
    public ContractEditWindow()
    {
        InitializeComponent();
    }

    public void SetViewModel(ContractEditViewModel viewModel)
    {
        DataContext = viewModel;
        Loaded += async (s, e) => await viewModel.LoadAsync();
    }
}
