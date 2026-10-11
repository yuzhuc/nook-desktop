using System;

namespace nook_desktop.Core.Navigation;

public interface INavigationService
{
    object? Current { get; }

    bool CanGoBack { get; }

    event EventHandler<object?>? Navigated;

    void NavigateTo(object viewModel);

    void GoBack();
}
