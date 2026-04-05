using System;
using System.Collections.Generic;
using YomiYa.Features;
using YomiYa.Features.Navigation;
using YomiYa.Features.Reader;

namespace YomiYa.Core.Navigation;

public static class NavigationHelper
{
    private static ViewModelBase _currentViewModel;

    private static readonly Stack<ViewModelBase> NavigationStack = new();

    static NavigationHelper()
    {
        _currentViewModel = new SideBarMenuViewModel();
        NavigationStack.Push(_currentViewModel);
    }

    public static ViewModelBase CurrentViewModel
    {
        get => _currentViewModel;
        private set
        {
            if (_currentViewModel == value) return;
            _currentViewModel = value;
            OnCurrentViewModelChanged?.Invoke(_currentViewModel);
        }
    }

    public static event Action<ViewModelBase>? OnCurrentViewModelChanged;

    public static void NavigateTo(ViewModelBase? viewModel)
    {
        if (viewModel is null) return;

        if (NavigationStack.Count == 0 || NavigationStack.Peek() != viewModel) NavigationStack.Push(viewModel);

        CurrentViewModel = viewModel;
    }

    public static void GoBack()
    {
        if (NavigationStack.Count <= 1) return;
        NavigationStack.Pop();
        CurrentViewModel = NavigationStack.Peek();
    }

    [Obsolete("Obsolete")]
    public static void OpenReader()
    {
        try
        {
            var reader = new ReaderView
            {
                DataContext = new ReaderViewModel()
            };

            reader.Show();
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.Message);
        }
    }
}