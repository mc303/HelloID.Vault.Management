using CommunityToolkit.Mvvm.ComponentModel;

namespace HelloID.Vault.Services;

/// <summary>
/// Service for handling navigation between different views in the application.
/// </summary>
public interface INavigationService
{
    /// <summary>
    /// Event raised when navigation occurs.
    /// </summary>
    event EventHandler<ObservableObject>? NavigationChanged;

    /// <summary>
    /// Navigates to the specified ViewModel type.
    /// </summary>
    /// <typeparam name="TViewModel">The type of ViewModel to navigate to.</typeparam>
    void NavigateTo<TViewModel>() where TViewModel : ObservableObject;

    /// <summary>
    /// Navigates back to the previous view.
    /// </summary>
    void NavigateBack();
}
