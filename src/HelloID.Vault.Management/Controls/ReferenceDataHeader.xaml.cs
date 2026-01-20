using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace HelloID.Vault.Management.Controls;

/// <summary>
/// Standard header component for reference data views.
/// Provides title, count, search, and action buttons in a consistent layout.
/// </summary>
public partial class ReferenceDataHeader : UserControl
{
    public ReferenceDataHeader()
    {
        InitializeComponent();
    }

    #region Title Dependency Property

    public static readonly DependencyProperty TitleProperty =
        DependencyProperty.Register(
            nameof(Title),
            typeof(string),
            typeof(ReferenceDataHeader),
            new PropertyMetadata(string.Empty));

    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    #endregion

    #region Count Dependency Property

    public static readonly DependencyProperty CountProperty =
        DependencyProperty.Register(
            nameof(Count),
            typeof(string),
            typeof(ReferenceDataHeader),
            new PropertyMetadata(string.Empty));

    public string Count
    {
        get => (string)GetValue(CountProperty);
        set => SetValue(CountProperty, value);
    }

    #endregion

    #region SearchPlaceholder Dependency Property

    public static readonly DependencyProperty SearchPlaceholderProperty =
        DependencyProperty.Register(
            nameof(SearchPlaceholder),
            typeof(string),
            typeof(ReferenceDataHeader),
            new PropertyMetadata("Search..."));

    public string SearchPlaceholder
    {
        get => (string)GetValue(SearchPlaceholderProperty);
        set => SetValue(SearchPlaceholderProperty, value);
    }

    #endregion

    #region SearchText Dependency Property

    public static readonly DependencyProperty SearchTextProperty =
        DependencyProperty.Register(
            nameof(SearchText),
            typeof(string),
            typeof(ReferenceDataHeader),
            new PropertyMetadata(string.Empty));

    public string SearchText
    {
        get => (string)GetValue(SearchTextProperty);
        set => SetValue(SearchTextProperty, value);
    }

    #endregion

    #region RefreshCommand Dependency Property

    public static readonly DependencyProperty RefreshCommandProperty =
        DependencyProperty.Register(
            nameof(RefreshCommand),
            typeof(ICommand),
            typeof(ReferenceDataHeader),
            new PropertyMetadata(null));

    public ICommand? RefreshCommand
    {
        get => (ICommand?)GetValue(RefreshCommandProperty);
        set => SetValue(RefreshCommandProperty, value);
    }

    #endregion

    #region AddCommand Dependency Property

    public static readonly DependencyProperty AddCommandProperty =
        DependencyProperty.Register(
            nameof(AddCommand),
            typeof(ICommand),
            typeof(ReferenceDataHeader),
            new PropertyMetadata(null));

    public ICommand? AddCommand
    {
        get => (ICommand?)GetValue(AddCommandProperty);
        set => SetValue(AddCommandProperty, value);
    }

    #endregion

    #region ResetCommand Dependency Property

    public static readonly DependencyProperty ResetCommandProperty =
        DependencyProperty.Register(
            nameof(ResetCommand),
            typeof(ICommand),
            typeof(ReferenceDataHeader),
            new PropertyMetadata(null));

    public ICommand? ResetCommand
    {
        get => (ICommand?)GetValue(ResetCommandProperty);
        set => SetValue(ResetCommandProperty, value);
    }

    #endregion
}
