using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Navigation;
using HelloID.Vault.Management.ViewModels;

namespace HelloID.Vault.Management.Views;

/// <summary>
/// Interaction logic for SettingsView.xaml
/// </summary>
public partial class SettingsView : UserControl
{
    private SettingsViewModel? ViewModel => DataContext as SettingsViewModel;

    public SettingsView()
    {
        InitializeComponent();
        Loaded += SettingsView_Loaded;
        Unloaded += SettingsView_Unloaded;
    }

    private void SettingsView_Loaded(object sender, RoutedEventArgs e)
    {
        // Populate password boxes from ViewModel (one-time load)
        if (ViewModel != null)
        {
            TursoAuthTokenPasswordBox.Password = ViewModel.TursoAuthToken;
            TursoPlatformApiTokenPasswordBox.Password = ViewModel.TursoPlatformApiToken;
            ConnectionPasswordBox.Password = ViewModel.SupabaseConnectionString;

            // Subscribe to property changes to sync PasswordBox <-> TextBox
            ViewModel.PropertyChanged += OnViewModelPropertyChanged;
        }
    }

    private void SettingsView_Unloaded(object sender, RoutedEventArgs e)
    {
        if (ViewModel != null)
        {
            ViewModel.PropertyChanged -= OnViewModelPropertyChanged;
        }
    }

    /// <summary>
    /// Handles ViewModel property changes to sync between PasswordBox and TextBox.
    /// </summary>
    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (ViewModel == null) return;

        switch (e.PropertyName)
        {
            case nameof(ViewModel.ShowSupabasePassword):
                SyncSupabasePasswordVisibility(ViewModel.ShowSupabasePassword);
                break;
            case nameof(ViewModel.ShowTursoAuthToken):
                SyncTursoAuthTokenVisibility(ViewModel.ShowTursoAuthToken);
                break;
            case nameof(ViewModel.ShowTursoPlatformToken):
                SyncTursoPlatformTokenVisibility(ViewModel.ShowTursoPlatformToken);
                break;
        }
    }

    /// <summary>
    /// Syncs the Supabase password between PasswordBox and TextBox when visibility toggles.
    /// </summary>
    private void SyncSupabasePasswordVisibility(bool isVisible)
    {
        if (ViewModel == null) return;

        if (!isVisible)
        {
            // About to hide TextBox, show PasswordBox - sync ViewModel -> PasswordBox
            ConnectionPasswordBox.Password = ViewModel.SupabaseConnectionString;
        }
        // When showing TextBox, the binding automatically syncs PasswordBox -> ViewModel
    }

    /// <summary>
    /// Syncs the Turso auth token between PasswordBox and TextBox when visibility toggles.
    /// </summary>
    private void SyncTursoAuthTokenVisibility(bool isVisible)
    {
        if (ViewModel == null) return;

        if (!isVisible)
        {
            // About to hide TextBox, show PasswordBox - sync ViewModel -> PasswordBox
            TursoAuthTokenPasswordBox.Password = ViewModel.TursoAuthToken;
        }
        // When showing TextBox, the binding automatically syncs PasswordBox -> ViewModel
    }

    /// <summary>
    /// Syncs the Turso platform token between PasswordBox and TextBox when visibility toggles.
    /// </summary>
    private void SyncTursoPlatformTokenVisibility(bool isVisible)
    {
        if (ViewModel == null) return;

        if (!isVisible)
        {
            // About to hide TextBox, show PasswordBox - sync ViewModel -> PasswordBox
            TursoPlatformApiTokenPasswordBox.Password = ViewModel.TursoPlatformApiToken;
        }
        // When showing TextBox, the binding automatically syncs PasswordBox -> ViewModel
    }

    /// <summary>
    /// Handles password changes in the Supabase PasswordBox (manual binding).
    /// </summary>
    private void OnPasswordChanged(object sender, RoutedEventArgs e)
    {
        if (sender is PasswordBox passwordBox && ViewModel != null)
        {
            ViewModel.SupabaseConnectionString = passwordBox.Password;
        }
    }

    /// <summary>
    /// Handles password changes in the Turso Auth Token PasswordBox (manual binding).
    /// </summary>
    private void OnTursoPasswordChanged(object sender, RoutedEventArgs e)
    {
        if (sender is PasswordBox passwordBox && ViewModel != null)
        {
            ViewModel.TursoAuthToken = passwordBox.Password;
        }
    }

    /// <summary>
    /// Handles password changes in the Turso Platform API Token PasswordBox (manual binding).
    /// </summary>
    private void OnTursoPlatformApiTokenPasswordChanged(object sender, RoutedEventArgs e)
    {
        if (sender is PasswordBox passwordBox && ViewModel != null)
        {
            ViewModel.TursoPlatformApiToken = passwordBox.Password;
        }
    }

    /// <summary>
    /// Handles help link navigation requests.
    /// </summary>
    private void OnHelpLinkRequestNavigate(object sender, RequestNavigateEventArgs e)
    {
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = e.Uri.AbsoluteUri,
                UseShellExecute = true
            });
            e.Handled = true;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Could not open link: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
