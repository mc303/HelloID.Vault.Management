using System.IO;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Caching.Memory;
using HelloID.Vault.Data;
using HelloID.Vault.Data.Connection;
using HelloID.Vault.Data.Repositories;
using HelloID.Vault.Data.Repositories.Interfaces;
using HelloID.Vault.Management.Views;
using HelloID.Vault.Management.Views.ReferenceData;
using HelloID.Vault.Management.ViewModels;
using HelloID.Vault.Management.ViewModels.Persons;
using HelloID.Vault.Management.ViewModels.Contracts;
using HelloID.Vault.Management.Views.Contracts;
using HelloID.Vault.Management.ViewModels.Import;
using HelloID.Vault.Management.ViewModels.ReferenceData;
using HelloID.Vault.Management.Services;
using HelloID.Vault.Services;
using HelloID.Vault.Services.Database;
using HelloID.Vault.Services.Interfaces;

namespace HelloID.Vault.Management;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    private readonly IHost _host;

    public App()
    {
        _host = Host.CreateDefaultBuilder()
            .ConfigureServices((context, services) =>
            {
                ConfigureServices(services);
            })
            .Build();

        // Set up global exception handlers
        SetupExceptionHandlers();
    }

    /// <summary>
    /// Configures global exception handlers for the application.
    /// </summary>
    private void SetupExceptionHandlers()
    {
        // UI thread exceptions
        DispatcherUnhandledException += OnDispatcherUnhandledException;

        // Background thread exceptions
        AppDomain.CurrentDomain.UnhandledException += OnCurrentDomainUnhandledException;

        // Unobserved task exceptions
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
    }

    /// <summary>
    /// Handles unhandled exceptions on the UI thread.
    /// </summary>
    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        LogException(e.Exception, "UI Thread");

        e.Handled = true; // Prevent application from crashing

        // Show error to user
        MessageBox.Show(
            $"An unexpected error occurred:\n\n{e.Exception.Message}\n\nThe application will continue running.",
            "Error",
            MessageBoxButton.OK,
            MessageBoxImage.Error);
    }

    /// <summary>
    /// Handles unhandled exceptions on background threads.
    /// </summary>
    private void OnCurrentDomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception exception)
        {
            LogException(exception, "Background Thread");
        }

        // Note: We cannot prevent the application from terminating here
        // Log the exception for debugging purposes
    }

    /// <summary>
    /// Handles unobserved task exceptions.
    /// </summary>
    private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        LogException(e.Exception, "Task");

        // Mark as observed to prevent process termination
        e.SetObserved();
    }

    /// <summary>
    /// Logs exception details for debugging.
    /// </summary>
    private void LogException(Exception exception, string source)
    {
        var logMessage = $"[{source}] {exception.GetType().Name}: {exception.Message}\n" +
                         $"Stack Trace: {exception.StackTrace}";

        System.Diagnostics.Debug.WriteLine(logMessage);

        // TODO: Write to log file or logging service
    }

    /// <summary>
    /// Gets the service provider for resolving dependencies.
    /// </summary>
    public IServiceProvider Services => _host.Services;

    private void ConfigureServices(IServiceCollection services)
    {
        // Database configuration
        var baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
        var dbPath = Path.Combine(baseDirectory, "db", "vault.db");
        var schemaPath = Path.Combine(baseDirectory, "db", "sqlite_schema.sql");

        // Memory Cache
        services.AddSingleton<IMemoryCache, MemoryCache>();

        services.AddSingleton<ISqliteConnectionFactory>(new SqliteConnectionFactory(dbPath));
        services.AddSingleton(sp => new DatabaseInitializer(
            sp.GetRequiredService<ISqliteConnectionFactory>(),
            dbPath,
            schemaPath));

        // Repositories
        services.AddSingleton<IPersonRepository, PersonRepository>();
        services.AddSingleton<IContractRepository, ContractRepository>();
        services.AddSingleton<IContactRepository, ContactRepository>();
        services.AddSingleton<IDepartmentRepository, DepartmentRepository>();
        services.AddSingleton<ILocationRepository, LocationRepository>();
        services.AddSingleton<ITitleRepository, TitleRepository>();
        services.AddSingleton<IDivisionRepository, DivisionRepository>();
        services.AddSingleton<ITeamRepository, TeamRepository>();
        services.AddSingleton<IOrganizationRepository, OrganizationRepository>();
        services.AddSingleton<IEmployerRepository, EmployerRepository>();
        services.AddSingleton<ICostCenterRepository, CostCenterRepository>();
        services.AddSingleton<ICostBearerRepository, CostBearerRepository>();
        services.AddSingleton<ICustomFieldRepository, CustomFieldRepository>();
        services.AddSingleton<IPrimaryContractConfigRepository, PrimaryContractConfigRepository>();
        services.AddSingleton<ISourceSystemRepository, SourceSystemRepository>();

        // Services
        services.AddSingleton<INavigationService, NavigationService>();
        services.AddSingleton<IPersonService, PersonService>();
        services.AddSingleton<IVaultImportService, VaultImportService>();
        services.AddSingleton<IReferenceDataService, ReferenceDataService>();
        services.AddSingleton<IContractService, ContractService>();
        services.AddSingleton<IUserPreferencesService, UserPreferencesService>();
        services.AddSingleton<IPrimaryManagerService, PrimaryManagerService>();
        services.AddSingleton<IDialogService, DialogService>();
        services.AddSingleton<IColumnLayoutManager, ColumnLayoutManager>();
        services.AddSingleton<IDatabaseManager, DatabaseManager>();

        // ViewModels
        services.AddSingleton<MainWindowViewModel>();
        services.AddTransient<PersonsViewModel>();
        services.AddTransient<ContractsViewModel>();
        services.AddTransient<ImportViewModel>();

        // Reference Data ViewModels
        services.AddTransient<DepartmentsViewModel>();
        services.AddTransient<LocationsViewModel>();
        services.AddTransient<TitlesViewModel>();
        services.AddTransient<CostCentersViewModel>();
        services.AddTransient<CostBearersViewModel>();
        services.AddTransient<EmployersViewModel>();
        services.AddTransient<TeamsViewModel>();
        services.AddTransient<DivisionsViewModel>();
        services.AddTransient<OrganizationsViewModel>();
        services.AddTransient<ContactsViewModel>();
        services.AddTransient<CustomFieldsViewModel>();
        services.AddTransient<CustomFieldEditViewModel>();
        services.AddTransient<PrimaryContractConfigViewModel>();
        services.AddTransient<SourceSystemsViewModel>();
        services.AddTransient<PrimaryManagerAdminViewModel>();

        // Views
        services.AddSingleton<MainWindow>();
        services.AddTransient<SourceSystemsView>();
    }

    protected override async void OnStartup(StartupEventArgs e)
    {
        await _host.StartAsync();

        try
        {
            // Initialize database
            var dbInitializer = _host.Services.GetRequiredService<DatabaseInitializer>();
            await dbInitializer.InitializeAsync();

            // Load user preferences
            var preferencesService = _host.Services.GetRequiredService<IUserPreferencesService>();
            await preferencesService.LoadAsync();

            // Warm up reference data cache in background (fire and forget)
            var referenceDataService = _host.Services.GetRequiredService<IReferenceDataService>();
            _ = Task.Run(async () =>
            {
                try
                {
                    await referenceDataService.WarmupCacheAsync();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Cache warmup failed: {ex.Message}");
                }
            });

            // Show main window
            var mainWindow = _host.Services.GetRequiredService<MainWindow>();
            var mainWindowViewModel = _host.Services.GetRequiredService<MainWindowViewModel>();

            // Initialize dialog service with dispatcher
            var dialogService = (HelloID.Vault.Management.Services.DialogService)_host.Services.GetRequiredService<IDialogService>();
            dialogService.SetDispatcher(mainWindow.Dispatcher);

            mainWindow.DataContext = mainWindowViewModel;
            mainWindow.Show();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Failed to start application: {ex.Message}",
                "Startup Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);

            Current.Shutdown();
        }

        base.OnStartup(e);
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        await _host.StopAsync();
        _host.Dispose();
        base.OnExit(e);
    }
}
