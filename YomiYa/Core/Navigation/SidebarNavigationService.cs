using System;

namespace YomiYa.Core.Navigation;

public sealed class SidebarNavigationService
{
    public event Action<Type>? NavigationRequested;
    public void NavigateTo<TViewModel>() => NavigationRequested?.Invoke(typeof(TViewModel));
}
