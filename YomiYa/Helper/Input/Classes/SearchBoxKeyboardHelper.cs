using System;
using Avalonia.Controls;
using Avalonia.Input;
using YomiYa.Helper.Input.Interfaces;

namespace YomiYa.Helper.Input.Classes;

public class SearchBoxKeyboardHelper : IDisposable
{
    private readonly TextBox _targetControl;
    private readonly ISearchableByKeyboard _viewModel;

    public SearchBoxKeyboardHelper(TextBox targetControl, ISearchableByKeyboard viewModel)
    {
        _targetControl = targetControl;
        _viewModel = viewModel;

        _targetControl.KeyDown += OnKeyDownHandler;
    }

    private void OnKeyDownHandler(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;
        if (_viewModel.SearchCommand.CanExecute(null)) _viewModel.SearchCommand.Execute(null);
        e.Handled = true;
    }

    public void Dispose()
    {
        _targetControl.KeyDown -= OnKeyDownHandler;
        GC.SuppressFinalize(this);
    }
}