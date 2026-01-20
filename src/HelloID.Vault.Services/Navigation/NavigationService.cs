using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.Generic;
using System.Diagnostics;

namespace HelloID.Vault.Services;

/// <summary>
/// Service for handling navigation between different views in application.
/// </summary>
public class NavigationService : INavigationService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly Stack<ObservableObject> _navigationStack = new();

    public NavigationService(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
    }

    /// <inheritdoc />
    public event EventHandler<ObservableObject>? NavigationChanged;

    /// <inheritdoc />
    public void NavigateTo<TViewModel>() where TViewModel : ObservableObject
    {
        var stopwatch = Stopwatch.StartNew();
        var viewName = typeof(TViewModel).Name.Replace("ViewModel", "");

        Debug.WriteLine($"[NAV-SERVICE] NavigateTo {viewName} START");

        var viewModel = _serviceProvider.GetRequiredService<TViewModel>();
        _navigationStack.Push(viewModel);
        NavigationChanged?.Invoke(this, viewModel);

        Debug.WriteLine($"[NAV-SERVICE] NavigateTo {viewName} END: {stopwatch.ElapsedMilliseconds}ms");
    }

    /// <inheritdoc />
    public void NavigateBack()
    {
        if (_navigationStack.Count <= 1)
        {
            // Can't go back if there's only one item or none
            return;
        }

        _navigationStack.Pop(); // Remove current view

        if (_navigationStack.Count > 0)
        {
            var previousViewModel = _navigationStack.Peek();
            NavigationChanged?.Invoke(this, previousViewModel);
        }
    }
}
