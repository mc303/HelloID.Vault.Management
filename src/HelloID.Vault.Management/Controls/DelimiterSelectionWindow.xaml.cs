using System.Globalization;
using System.Windows;

namespace HelloID.Vault.Management.Controls;

/// <summary>
/// Dialog for choosing the CSV export delimiter, with an optional
/// "remember this choice" checkbox that persists the selection.
/// </summary>
public partial class DelimiterSelectionWindow : Window
{
    /// <summary>
    /// The chosen delimiter. Only valid when the dialog result is true.
    /// </summary>
    public char SelectedDelimiter { get; private set; } = ',';

    /// <summary>
    /// Whether the user asked to remember the choice.
    /// </summary>
    public bool RememberChoice => RememberCheckBox.IsChecked == true;

    public DelimiterSelectionWindow()
    {
        InitializeComponent();

        // Preselect the system list separator (semicolon on Dutch Windows)
        var systemSeparator = CultureInfo.CurrentCulture.TextInfo.ListSeparator;
        if (systemSeparator == ";")
        {
            SemicolonRadio.IsChecked = true;
        }
        else if (systemSeparator == "\t")
        {
            TabRadio.IsChecked = true;
        }
        else
        {
            CommaRadio.IsChecked = true;
        }
    }

    private char GetSelectedDelimiter()
    {
        if (SemicolonRadio.IsChecked == true)
        {
            return ';';
        }

        if (TabRadio.IsChecked == true)
        {
            return '\t';
        }

        return ',';
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        SelectedDelimiter = GetSelectedDelimiter();
        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}
