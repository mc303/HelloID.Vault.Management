using System.Windows.Controls;
using System.Windows.Input;
using HelloID.Vault.Core.Models.DTOs;
using HelloID.Vault.Management.ViewModels.Persons;

namespace HelloID.Vault.Management.Controls;

public partial class ManagerSearchControl : UserControl
{
    public ManagerSearchControl()
    {
        InitializeComponent();
    }

    private void ResultsList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is ListBox listbox && listbox.SelectedItem is PersonSearchResultDto person)
        {
            if (DataContext is ManagerSearchViewModel vm)
            {
                vm.SelectResultCommand.Execute(person);
            }
        }
    }
}
