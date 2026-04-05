using CommunityToolkit.Mvvm.Input;

namespace YomiYa.Helper.Input.Interfaces;

public interface ISearchableByKeyboard
{
    IRelayCommand SearchCommand { get; }
}