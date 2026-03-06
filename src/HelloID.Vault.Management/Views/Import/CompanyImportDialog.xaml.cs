using System.Windows;
using HelloID.Vault.Data.Connection;

namespace HelloID.Vault.Management.Views.Import;

/// <summary>
/// Interaction logic for CompanyImportDialog.xaml
/// </summary>
public partial class CompanyImportDialog : Window
{
    public ImportConfirmResult Result { get; private set; } = ImportConfirmResult.Cancel;

    public CompanyImportDialog(DatabaseType databaseType)
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
        else if (databaseType == DatabaseType.Turso)
        {
            // Disable backup button for Turso (no file backup possible)
            BackupAndOverwriteButton.IsEnabled = false;
            BackupAndOverwriteButton.ToolTip = "Database backup is not supported for Turso";

            // Update overwrite message for Turso
            OverwriteMessage.Text = "Delete database and recreate (all data will be lost)";
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