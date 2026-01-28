using System.Windows;
using HelloID.Vault.Management.ViewModels.Contracts;
using HelloID.Vault.Services.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace HelloID.Vault.Management.Views.Contracts;

public partial class AdvancedContractSearchWindow : Window
{
    public AdvancedContractSearchWindow()
    {
        InitializeComponent();
        var dialogService = ((App)Application.Current).Services.GetRequiredService<IDialogService>();
        DataContext = new AdvancedContractSearchViewModel(dialogService);
    }

    public AdvancedContractSearchViewModel ViewModel => (AdvancedContractSearchViewModel)DataContext;
}
