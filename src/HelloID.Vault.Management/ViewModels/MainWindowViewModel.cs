using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HelloID.Vault.Services;
using HelloID.Vault.Management.ViewModels.Persons;
using HelloID.Vault.Management.ViewModels.Contracts;
using HelloID.Vault.Management.ViewModels.Import;
using HelloID.Vault.Management.ViewModels.ReferenceData;
using HelloID.Vault.Services.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace HelloID.Vault.Management.ViewModels;

/// <summary>
/// ViewModel for main application window.
/// </summary>
public partial class MainWindowViewModel : ObservableObject
{
    private readonly INavigationService _navigationService;
    private readonly IServiceProvider _serviceProvider;
    private readonly IDialogService _dialogService;

    [ObservableProperty]
    private ObservableObject? _currentViewModel;

    [ObservableProperty]
    private string _title = "HR System";

    public MainWindowViewModel(INavigationService navigationService, IServiceProvider serviceProvider)
    {
        _navigationService = navigationService ?? throw new ArgumentNullException(nameof(navigationService));
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _dialogService = serviceProvider.GetRequiredService<IDialogService>();
        _navigationService.NavigationChanged += OnNavigationChanged;
    }

    private void OnNavigationChanged(object? sender, ObservableObject viewModel)
    {
        CurrentViewModel = viewModel;
    }

    /// <summary>
    /// Navigates to Persons view.
    /// </summary>
    [RelayCommand]
    private void NavigateToPersons()
    {
        var viewModel = _serviceProvider.GetRequiredService<PersonsViewModel>();
        CurrentViewModel = viewModel;
        _ = viewModel.InitializeAsync();
    }

    [RelayCommand]
    private void NavigateToContracts()
    {
        var viewModel = _serviceProvider.GetRequiredService<ContractsViewModel>();
        CurrentViewModel = viewModel;
        _ = viewModel.InitializeAsync();
    }

    /// <summary>
    /// Navigates to the Import view.
    /// </summary>
    [RelayCommand]
    private void NavigateToImport()
    {
        var viewModel = _serviceProvider.GetRequiredService<ImportViewModel>();
        CurrentViewModel = viewModel;
    }

    /// <summary>
    /// Navigates to the Departments view.
    /// </summary>
    [RelayCommand]
    private void NavigateToDepartments()
    {
        var viewModel = _serviceProvider.GetRequiredService<DepartmentsViewModel>();
        CurrentViewModel = viewModel;
        _ = viewModel.InitializeAsync();
    }

    /// <summary>
    /// Navigates to the Locations view.
    /// </summary>
    [RelayCommand]
    private void NavigateToLocations()
    {
        var viewModel = _serviceProvider.GetRequiredService<LocationsViewModel>();
        CurrentViewModel = viewModel;
        _ = viewModel.InitializeAsync();
    }

    /// <summary>
    /// Navigates to the Titles view.
    /// </summary>
    [RelayCommand]
    private void NavigateToTitles()
    {
        var viewModel = _serviceProvider.GetRequiredService<TitlesViewModel>();
        CurrentViewModel = viewModel;
        _ = viewModel.InitializeAsync();
    }

    /// <summary>
    /// Navigates to the Cost Centers view.
    /// </summary>
    [RelayCommand]
    private void NavigateToCostCenters()
    {
        var viewModel = _serviceProvider.GetRequiredService<CostCentersViewModel>();
        CurrentViewModel = viewModel;
        _ = viewModel.InitializeAsync();
    }

    /// <summary>
    /// Navigates to the Cost Bearers view.
    /// </summary>
    [RelayCommand]
    private void NavigateToCostBearers()
    {
        var viewModel = _serviceProvider.GetRequiredService<CostBearersViewModel>();
        CurrentViewModel = viewModel;
        _ = viewModel.InitializeAsync();
    }

    /// <summary>
    /// Navigates to the Employers view.
    /// </summary>
    [RelayCommand]
    private void NavigateToEmployers()
    {
        var viewModel = _serviceProvider.GetRequiredService<EmployersViewModel>();
        CurrentViewModel = viewModel;
        _ = viewModel.InitializeAsync();
    }

    /// <summary>
    /// Navigates to the Teams view.
    /// </summary>
    [RelayCommand]
    private void NavigateToTeams()
    {
        var viewModel = _serviceProvider.GetRequiredService<TeamsViewModel>();
        CurrentViewModel = viewModel;
        _ = viewModel.InitializeAsync();
    }

    /// <summary>
    /// Navigates to the Divisions view.
    /// </summary>
    [RelayCommand]
    private void NavigateToDivisions()
    {
        var viewModel = _serviceProvider.GetRequiredService<DivisionsViewModel>();
        CurrentViewModel = viewModel;
        _ = viewModel.InitializeAsync();
    }

    /// <summary>
    /// Navigates to the Organizations view.
    /// </summary>
    [RelayCommand]
    private void NavigateToOrganizations()
    {
        var viewModel = _serviceProvider.GetRequiredService<OrganizationsViewModel>();
        CurrentViewModel = viewModel;
        _ = viewModel.InitializeAsync();
    }

    /// <summary>
    /// Navigates to the Contacts view.
    /// </summary>
    [RelayCommand]
    private void NavigateToContacts()
    {
        var viewModel = _serviceProvider.GetRequiredService<ContactsViewModel>();
        CurrentViewModel = viewModel;
        _ = viewModel.InitializeAsync();
    }

    /// <summary>
    /// Navigates to the Custom Fields view.
    /// </summary>
    [RelayCommand]
    private void NavigateToCustomFields()
    {
        var viewModel = _serviceProvider.GetRequiredService<CustomFieldsViewModel>();
        CurrentViewModel = viewModel;
        _ = viewModel.InitializeAsync();
    }

    /// <summary>
    /// Navigates to the Source Systems view.
    /// </summary>
    [RelayCommand]
    private void NavigateToSourceSystems()
    {
        var viewModel = _serviceProvider.GetRequiredService<SourceSystemsViewModel>();
        CurrentViewModel = viewModel;
        _ = viewModel.InitializeAsync();
    }

    /// <summary>
    /// Navigates to the Primary Contract Configuration dialog.
    /// </summary>
    [RelayCommand]
    private async Task NavigateToPrimaryContractConfig()
    {
        try
        {
            var viewModel = _serviceProvider.GetRequiredService<PrimaryContractConfigViewModel>();
            CurrentViewModel = viewModel;
            await viewModel.LoadAsync();
        }
        catch (Exception ex)
        {
            _dialogService.ShowError($"Failed to open Primary Contract Configuration: {ex.Message}", "Error");
        }
    }

    /// <summary>
    /// Navigates to the Primary Manager Administration view.
    /// </summary>
    [RelayCommand]
    private async Task NavigateToPrimaryManagerAdmin()
    {
        try
        {
            var viewModel = _serviceProvider.GetRequiredService<PrimaryManagerAdminViewModel>();
            CurrentViewModel = viewModel;
            await viewModel.LoadStatisticsAsync();
        }
        catch (Exception ex)
        {
            _dialogService.ShowError($"Failed to open Primary Manager Administration: {ex.Message}", "Error");
        }
    }
}
