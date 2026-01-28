using System.Windows;
using System.Windows.Threading;
using HelloID.Vault.Services.Interfaces;

namespace HelloID.Vault.Management.Services;

/// <summary>
/// Default implementation of IDialogService using WPF MessageBox.
/// </summary>
public class DialogService : IDialogService
{
    private Dispatcher? _dispatcher;

    /// <summary>
    /// Sets the dispatcher for UI thread operations. Call this from App.xaml.cs after main window is created.
    /// </summary>
    public void SetDispatcher(Dispatcher dispatcher)
    {
        _dispatcher = dispatcher;
    }

    /// <summary>
    /// Shows an information message to the user.
    /// </summary>
    public void ShowInfo(string message, string title = "Information")
    {
        InvokeOnDispatcher(() =>
        {
            MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Information);
        });
    }

    /// <summary>
    /// Shows a warning message to the user.
    /// </summary>
    public void ShowWarning(string message, string title = "Warning")
    {
        InvokeOnDispatcher(() =>
        {
            MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Warning);
        });
    }

    /// <summary>
    /// Shows an error message to the user.
    /// </summary>
    public void ShowError(string message, string title = "Error")
    {
        InvokeOnDispatcher(() =>
        {
            MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Error);
        });
    }

    /// <summary>
    /// Shows a confirmation dialog and returns true if the user clicks Yes.
    /// </summary>
    public bool ShowConfirm(string message, string title = "Confirm")
    {
        bool result = false;
        InvokeOnDispatcher(() =>
        {
            result = MessageBox.Show(message, title, MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes;
        });
        return result;
    }

    /// <summary>
    /// Shows a confirmation dialog asynchronously and returns true if the user clicks Yes.
    /// </summary>
    public Task<bool> ShowConfirmAsync(string message, string title = "Confirm")
    {
        return Task.FromResult(ShowConfirm(message, title));
    }

    /// <summary>
    /// Invokes an action on the UI thread dispatcher.
    /// </summary>
    private void InvokeOnDispatcher(Action action)
    {
        if (_dispatcher != null)
        {
            if (_dispatcher.CheckAccess())
            {
                action();
            }
            else
            {
                _dispatcher.Invoke(action);
            }
        }
        else
        {
            // Fallback if dispatcher not set (should not happen in production)
            action();
        }
    }
}
