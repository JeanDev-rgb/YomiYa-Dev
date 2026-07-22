using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using YomiYa.Core.Localization;

namespace YomiYa.Features.Points;

public partial class PointsPageViewModel : ViewModelBase
{
    [ObservableProperty] private int _currentPoints;
    [ObservableProperty] private int _totalEarnedPoints;

    // Textos localizados
    [ObservableProperty] private string _pointsTitleText = string.Empty;
    [ObservableProperty] private string _pointsSubtitleText = string.Empty;
    [ObservableProperty] private string _yourPointsText = string.Empty;
    [ObservableProperty] private string _earnPointsText = string.Empty;
    [ObservableProperty] private string _redeemText = string.Empty;
    [ObservableProperty] private string _rewardsText = string.Empty;
    [ObservableProperty] private string _howToEarnText = string.Empty;
    [ObservableProperty] private string _dailyReadingText = string.Empty;
    [ObservableProperty] private string _dailyReadingDescText = string.Empty;
    [ObservableProperty] private string _completeChapterText = string.Empty;
    [ObservableProperty] private string _completeChapterDescText = string.Empty;
    [ObservableProperty] private string _addToLibraryText = string.Empty;
    [ObservableProperty] private string _addToLibraryDescText = string.Empty;
    [ObservableProperty] private string _comingSoonText = string.Empty;

    public ObservableCollection<RewardItem> Rewards { get; } = [];

    public PointsPageViewModel()
    {
        CurrentPoints = 0;
        TotalEarnedPoints = 0;
        UpdateLocalizedTexts();
        LoadRewards();
    }

    private void LoadRewards()
    {
        Rewards.Clear();
        Rewards.Add(new RewardItem
        {
            Name = LanguageHelper.GetText("RewardCustomTheme"),
            Description = LanguageHelper.GetText("RewardCustomThemeDesc"),
            Cost = 500,
            Icon = "🎨"
        });
        Rewards.Add(new RewardItem
        {
            Name = LanguageHelper.GetText("RewardAdFree"),
            Description = LanguageHelper.GetText("RewardAdFreeDesc"),
            Cost = 1000,
            Icon = "🚫"
        });
        Rewards.Add(new RewardItem
        {
            Name = LanguageHelper.GetText("RewardExclusive"),
            Description = LanguageHelper.GetText("RewardExclusiveDesc"),
            Cost = 2500,
            Icon = "⭐"
        });
    }

    [RelayCommand]
    private void RedeemReward(RewardItem reward)
    {
        if (CurrentPoints >= reward.Cost)
        {
            CurrentPoints -= reward.Cost;
        }
    }

    protected sealed override void UpdateLocalizedTexts()
    {
        PointsTitleText = LanguageHelper.GetText("PointsTitle");
        PointsSubtitleText = LanguageHelper.GetText("PointsSubtitle");
        YourPointsText = LanguageHelper.GetText("YourPoints");
        EarnPointsText = LanguageHelper.GetText("EarnPoints");
        RedeemText = LanguageHelper.GetText("Redeem");
        RewardsText = LanguageHelper.GetText("Rewards");
        HowToEarnText = LanguageHelper.GetText("HowToEarn");
        DailyReadingText = LanguageHelper.GetText("DailyReading");
        DailyReadingDescText = LanguageHelper.GetText("DailyReadingDesc");
        CompleteChapterText = LanguageHelper.GetText("CompleteChapter");
        CompleteChapterDescText = LanguageHelper.GetText("CompleteChapterDesc");
        AddToLibraryText = LanguageHelper.GetText("AddToLibrary");
        AddToLibraryDescText = LanguageHelper.GetText("AddToLibraryDesc");
        ComingSoonText = LanguageHelper.GetText("ComingSoon");
        LoadRewards();
    }
}

public class RewardItem
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int Cost { get; set; }
    public string Icon { get; set; } = string.Empty;
}
