using System.Windows;
using HelloID.Vault.Management.ViewModels.Import;

namespace HelloID.Vault.Management.Views.Import;

public partial class PersonSelectionDialog : Window
{
    private readonly PersonSelectionViewModel _viewModel;

    public PersonSelectionDialog(PersonSelectionViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = _viewModel;

        _viewModel.CloseRequested += OnCloseRequested;
    }

    public HashSet<string> GetSelectedPersonIds()
    {
        return _viewModel.GetSelectedPersonIds();
    }

    public bool ClearMissingManagerReferences => _viewModel.ClearMissingManagerReferences;
    public bool CascadeImportManagers => _viewModel.CascadeImportManagers;

    private void OnCloseRequested(bool result)
    {
        DialogResult = result;
        Close();
    }
}
