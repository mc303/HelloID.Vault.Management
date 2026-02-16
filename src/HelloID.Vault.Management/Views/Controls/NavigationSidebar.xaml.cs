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
        // Select first item by default (Persons)
        if (NavView.MenuItems.Count > 0)
        {
            NavView.SelectedItem = NavView.MenuItems[0];
        }
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
