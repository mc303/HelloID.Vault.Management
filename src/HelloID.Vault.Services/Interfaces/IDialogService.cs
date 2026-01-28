namespace HelloID.Vault.Services.Interfaces;

/// <summary>
/// Service for showing dialog boxes from ViewModels.
/// Enables better testability by abstracting MessageBox interactions.
/// </summary>
public interface IDialogService
{
    /// <summary>
    /// Shows an information message to the user.
    /// </summary>
    void ShowInfo(string message, string title = "Information");

    /// <summary>
    /// Shows a warning message to the user.
    /// </summary>
    void ShowWarning(string message, string title = "Warning");

    /// <summary>
    /// Shows an error message to the user.
    /// </summary>
    void ShowError(string message, string title = "Error");

    /// <summary>
    /// Shows a confirmation dialog and returns true if the user clicks Yes.
    /// </summary>
    bool ShowConfirm(string message, string title = "Confirm");

    /// <summary>
    /// Shows a confirmation dialog asynchronously and returns true if the user clicks Yes.
    /// </summary>
    Task<bool> ShowConfirmAsync(string message, string title = "Confirm");
}
