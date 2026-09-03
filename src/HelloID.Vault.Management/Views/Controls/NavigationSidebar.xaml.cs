using System.Windows;
using System.Windows.Controls;
using HelloID.Vault.Management.ViewModels;
using ModernWpf.Controls;

namespace HelloID.Vault.Management.Views.Controls;

/// <summary>
/// Interaction logic for NavigationSidebar.xaml
/// </summary>
public partial class NavigationSidebar : UserControl
{
    public NavigationSidebar()
    {
        InitializeComponent();
        Loaded += NavigationSidebar_Loaded;
    }

    private void NavigationSidebar_Loaded(object sender, RoutedEventArgs e)
    {
        // Select initial item based on MainWindowViewModel.InitialNavTag (default: Persons).
        // Used to land on Database Management when the database is unreachable.
        var targetTag = "Persons";
        if (Window.GetWindow(this)?.DataContext is MainWindowViewModel vm && !string.IsNullOrEmpty(vm.InitialNavTag))
        {
            targetTag = vm.InitialNavTag;
        }

        var targetItem = FindMenuItemByTag(NavView.MenuItems, targetTag) ?? NavView.MenuItems[0];
        NavView.SelectedItem = targetItem;
    }

    private static object? FindMenuItemByTag(System.Collections.IEnumerable menuItems, string tag)
    {
        foreach (var item in menuItems)
        {
            if (item is NavigationViewItem navItem)
            {
                if (navItem.Tag?.ToString() == tag)
                {
                    return navItem;
                }

                // Search nested menu items (e.g., Administration children)
                if (navItem.MenuItems != null)
                {
                    var nested = FindMenuItemByTag(navItem.MenuItems, tag);
                    if (nested != null)
                    {
                        return nested;
                    }
                }
            }
        }
        return null;
    }

    private void NavView_SelectionChanged(ModernWpf.Controls.NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (args.SelectedItem is NavigationViewItem selectedItem)
        {
            var tag = selectedItem.Tag?.ToString();

            // Store the selected item to maintain selection after navigation
            var currentSelection = selectedItem;

            // Get MainWindowViewModel from ancestor Window
            var mainWindow = Window.GetWindow(this);
            if (mainWindow?.DataContext is MainWindowViewModel viewModel)
            {
                switch (tag)
                {
                    case "Persons":
                        viewModel.NavigateToPersonsCommand.Execute(null);
                        break;
                    case "Contracts":
                        viewModel.NavigateToContractsCommand.Execute(null);
                        break;
                    case "ImportData":
                        viewModel.NavigateToImportCommand.Execute(null);
                        break;
                    case "Departments":
                        viewModel.NavigateToDepartmentsCommand.Execute(null);
                        break;
                    case "Locations":
                        viewModel.NavigateToLocationsCommand.Execute(null);
                        break;
                    case "Titles":
                        viewModel.NavigateToTitlesCommand.Execute(null);
                        break;
                    case "CostCenters":
                        viewModel.NavigateToCostCentersCommand.Execute(null);
                        break;
                    case "CostBearers":
                        viewModel.NavigateToCostBearersCommand.Execute(null);
                        break;
                    case "Employers":
                        viewModel.NavigateToEmployersCommand.Execute(null);
                        break;
                    case "Teams":
                        viewModel.NavigateToTeamsCommand.Execute(null);
                        break;
                    case "Divisions":
                        viewModel.NavigateToDivisionsCommand.Execute(null);
                        break;
                    case "Organizations":
                        viewModel.NavigateToOrganizationsCommand.Execute(null);
                        break;
                    case "Contacts":
                        viewModel.NavigateToContactsCommand.Execute(null);
                        break;
                    case "PersonCustomFieldData":
                        viewModel.NavigateToPersonCustomFieldDataCommand.Execute(null);
                        break;
                    case "ContractCustomFieldData":
                        viewModel.NavigateToContractCustomFieldDataCommand.Execute(null);
                        break;
                    case "CustomFields":
                        viewModel.NavigateToCustomFieldsCommand.Execute(null);
                        break;
                    case "SourceSystems":
                        viewModel.NavigateToSourceSystemsCommand.Execute(null);
                        break;
                    case "PrimaryContractConfig":
                        viewModel.NavigateToPrimaryContractConfigCommand.Execute(null);
                        break;
                    case "PrimaryManagerAdmin":
                        viewModel.NavigateToPrimaryManagerAdminCommand.Execute(null);
                        break;
                    case "AppSettings":
                        viewModel.NavigateToSettingsCommand.Execute(null);
                        break;
                }

                // Ensure selection persists after navigation
                NavView.SelectedItem = currentSelection;
            }
        }
    }
}
