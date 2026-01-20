using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Diagnostics;
using HelloID.Vault.Management.ViewModels.Persons;
using ModernWpf.Controls;

namespace HelloID.Vault.Management.Views.Persons;

/// <summary>
/// Interaction logic for PersonsView.xaml
/// </summary>
public partial class PersonsView : UserControl
{
    public PersonsView()
    {
        InitializeComponent();
        Loaded += PersonsView_Loaded;
    }

    /// <summary>
    /// Called when view is loaded. Attaches scroll event for infinite scrolling.
    /// Note: ViewModel is already initialized by navigation command.
    /// </summary>
    private async void PersonsView_Loaded(object sender, RoutedEventArgs e)
    {
        var stopwatch = Stopwatch.StartNew();
        Debug.WriteLine($"[VIEW-LOAD] PersonsView Loaded START");

        if (DataContext is PersonsViewModel viewModel)
        {
            // Attach scroll event handler for infinite scrolling
            if (PersonsListBox != null)
            {
                var scrollViewer = GetScrollViewer(PersonsListBox);
                if (scrollViewer != null)
                {
                    scrollViewer.ScrollChanged += ScrollViewer_ScrollChanged;
                }
            }
        }

        Debug.WriteLine($"[VIEW-LOAD] PersonsView Loaded END: {stopwatch.ElapsedMilliseconds}ms");
    }

    /// <summary>
    /// Detects when user scrolls near the bottom and loads more data.
    /// </summary>
    private async void ScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        var scrollViewer = sender as ScrollViewer;
        if (scrollViewer == null) return;

        // Calculate how close we are to the bottom (trigger when within 200 pixels)
        var threshold = 200.0;
        var distanceFromBottom = scrollViewer.ScrollableHeight - scrollViewer.VerticalOffset;

        if (distanceFromBottom < threshold)
        {
            // Load more data when near bottom
            if (DataContext is PersonsViewModel viewModel)
            {
                await viewModel.LoadMoreAsync();
            }
        }
    }

    /// <summary>
    /// Helper method to find the ScrollViewer inside a ListBox.
    /// </summary>
    private ScrollViewer? GetScrollViewer(DependencyObject element)
    {
        if (element is ScrollViewer scrollViewer)
        {
            return scrollViewer;
        }

        for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(element); i++)
        {
            var child = System.Windows.Media.VisualTreeHelper.GetChild(element, i);
            var result = GetScrollViewer(child);
            if (result != null)
            {
                return result;
            }
        }

        return null;
    }

    /// <summary>
    /// Handles the TextChanged event for the AutoSuggestBox.
    /// Forces the binding to update the ViewModel's SearchText property.
    /// </summary>
    private void AutoSuggestBox_TextChanged(ModernWpf.Controls.AutoSuggestBox sender, ModernWpf.Controls.AutoSuggestBoxTextChangedEventArgs args)
    {
        // Force the binding to update the source (ViewModel property)
        var bindingExpression = sender.GetBindingExpression(ModernWpf.Controls.AutoSuggestBox.TextProperty);
        bindingExpression?.UpdateSource();

        System.Diagnostics.Debug.WriteLine($"[PersonsView] AutoSuggestBox_TextChanged called, Text='{sender.Text}'");
    }
}
