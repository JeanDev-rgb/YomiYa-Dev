using CommunityToolkit.Mvvm.Input;

namespace YomiYa.Helper.Input.Interfaces;

public interface IKeyboardNavigable
{
    #region Commands

    IRelayCommand NextPageCommand { get; }
    IRelayCommand PreviousPageCommand { get; }
    IRelayCommand ToggleReadingModeCommand { get; }
    IRelayCommand ToggleReadingWidthCommand { get; }
    
    #endregion
}