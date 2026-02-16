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
    }

    /// <summary>
    /// Handles password changes in the PasswordBox (manual binding).
    /// </summary>
    private void OnPasswordChanged(object sender, RoutedEventArgs e)
    {
        if (sender is PasswordBox passwordBox && ViewModel != null)
        {
            ViewModel.SupabaseConnectionString = passwordBox.Password;
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
