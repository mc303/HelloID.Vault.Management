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
        if (databaseType.IsPostgreSqlBackend())
        {
            // Disable backup button for PostgreSQL
            BackupAndOverwriteButton.IsEnabled = false;
            BackupAndOverwriteButton.ToolTip = "Database backup is not supported for PostgreSQL";

            // Update overwrite message for PostgreSQL
            OverwriteMessage.Text = "Drop all tables and reimport (database is preserved)";
        }
        else if (databaseType == DatabaseType.Turso)
        {
            // Backup downloads the cloud database to a local SQLite file
            BackupAndOverwriteButton.ToolTip = "Downloads the current database to a local backup file before importing";

            // Update overwrite message for Turso
            OverwriteMessage.Text = "Replaces the entire cloud database with the imported data (all data will be lost)";
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
