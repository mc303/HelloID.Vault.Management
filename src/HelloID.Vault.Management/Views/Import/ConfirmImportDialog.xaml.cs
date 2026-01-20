using System.Windows;

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

    public ConfirmImportDialog()
    {
        InitializeComponent();
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
