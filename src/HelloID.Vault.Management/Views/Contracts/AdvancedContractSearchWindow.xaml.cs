using System.Windows;
using HelloID.Vault.Management.ViewModels.Contracts;

namespace HelloID.Vault.Management.Views.Contracts;

public partial class AdvancedContractSearchWindow : Window
{
    public AdvancedContractSearchWindow()
    {
        InitializeComponent();
        DataContext = new AdvancedContractSearchViewModel();
    }

    public AdvancedContractSearchViewModel ViewModel => (AdvancedContractSearchViewModel)DataContext;
}
