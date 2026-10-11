using System;
using System.Collections.Generic;
using nook_desktop.Core.Mvvm;

namespace nook_desktop.Core.Navigation;

public sealed class NavigationService : INavigationService
{
    private readonly Stack<object> _history = new();

    public object? Current { get; private set; }

    public bool CanGoBack => _history.Count > 0;

    public event EventHandler<object?>? Navigated;

    public void NavigateTo(object viewModel)
    {
        if (viewModel is null)
        {
            throw new ArgumentNullException(nameof(viewModel));
        }

        if (Current is ViewModelBase leaving)
        {
            leaving.OnNavigatedFrom();
        }

        if (Current is not null)
        {
            _history.Push(Current);
        }

        Current = viewModel;

        if (viewModel is ViewModelBase entering)
        {
            entering.OnNavigatedTo();
        }

        Navigated?.Invoke(this, Current);
    }

    public void GoBack()
    {
        if (_history.Count == 0)
        {
            return;
        }

        if (Current is ViewModelBase leaving)
        {
            leaving.OnNavigatedFrom();
        }

        Current = _history.Pop();

        if (Current is ViewModelBase entering)
        {
            entering.OnNavigatedTo();
        }

        Navigated?.Invoke(this, Current);
    }
}
