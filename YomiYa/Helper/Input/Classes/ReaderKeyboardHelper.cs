using System;
using Avalonia.Controls;
using Avalonia.Input;
using YomiYa.Helper.Input.Interfaces;

namespace YomiYa.Helper.Input.Classes;

public class ReaderKeyboardHelper : IDisposable
{
    private readonly TopLevel _targetControl;
    private readonly IKeyboardNavigable _viewModel;

    public ReaderKeyboardHelper(TopLevel targetControl, IKeyboardNavigable viewModel)
    {
        _targetControl = targetControl;
        _viewModel = viewModel;
        _targetControl.KeyDown += OnKeyDownHandler;
    }

    public void Dispose()
    {
        _targetControl.KeyDown -= OnKeyDownHandler;
        GC.SuppressFinalize(this);
    }

    private void OnKeyDownHandler(object? sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Right:
                if (_viewModel.NextPageCommand.CanExecute(null)) _viewModel.NextPageCommand.Execute(null);
                break;
            case Key.Left:
                if (_viewModel.PreviousPageCommand.CanExecute(null)) _viewModel.PreviousPageCommand.Execute(null);
                break;
            case Key.M:
                if (_viewModel.ToggleReadingModeCommand.CanExecute(null))
                    _viewModel.ToggleReadingModeCommand.Execute(null);
                break;
            case Key.W:
                if (_viewModel.ToggleReadingWidthCommand.CanExecute(null))
                    _viewModel.ToggleReadingWidthCommand.Execute(null);
                break;
            case Key.Escape:
                if (_targetControl is Window window)
                    window.Close();
                break;
            default:
                Console.WriteLine(e.Key);
                break;
        }

        e.Handled = true;
    }
}