using System.Windows;
using HelloID.Vault.Data.Connection;

namespace HelloID.Vault.Management.Views.Import;

/// <summary>
/// Dialog result for import confirmation.
/// </summary>
public enum ImportConfirmResult
{
    Cancel,
    BackupAndOverwrite,
    OverwriteWithoutBackup
}

/// <summary>
/// Interaction logic for ConfirmImportDialog.xaml
/// </summary>
public partial class ConfirmImportDialog : Window
{
    public ImportConfirmResult Result { get; private set; } = ImportConfirmResult.Cancel;

    public ConfirmImportDialog(DatabaseType databaseType)
    {
        InitializeComponent();
        ApplyDatabaseTypeSettings(databaseType);
    }

    private void ApplyDatabaseTypeSettings(DatabaseType databaseType)
    {
        if (databaseType == DatabaseType.PostgreSql)
        {
            // Disable backup button for PostgreSQL
            BackupAndOverwriteButton.IsEnabled = false;
            BackupAndOverwriteButton.ToolTip = "Database backup is not supported for PostgreSQL";

            // Update overwrite message for PostgreSQL
            OverwriteMessage.Text = "Drop all tables and reimport (database is preserved)";
        }
        // SQLite uses default XAML values
    }

    private void BackupAndOverwrite_Click(object sender, RoutedEventArgs e)
    {
        Result = ImportConfirmResult.BackupAndOverwrite;
        DialogResult = true;
        Close();
    }

    private void OverwriteWithoutBackup_Click(object sender, RoutedEventArgs e)
    {
        Result = ImportConfirmResult.OverwriteWithoutBackup;
        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        Result = ImportConfirmResult.Cancel;
        DialogResult = false;
        Close();
    }
}
