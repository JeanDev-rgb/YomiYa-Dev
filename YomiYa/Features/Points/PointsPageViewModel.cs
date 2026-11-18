using System;
using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;
using YomiYa.Core.Localization;

namespace YomiYa.Features.Points;

public partial class PointsPageViewModel : ViewModelBase
{
    [ObservableProperty] private int _currentPoints;
    [ObservableProperty] private int _totalEarnedPoints;
    [ObservableProperty] private string _pointsTitleText = string.Empty;
    [ObservableProperty] private string _pointsSubtitleText = string.Empty;
    [ObservableProperty] private string _yourPointsText = string.Empty;
    [ObservableProperty] private string _earnPointsText = string.Empty;
    [ObservableProperty] private string _redeemText = string.Empty;
    [ObservableProperty] private string _rewardsText = string.Empty;
    [ObservableProperty] private string _rewardsUnavailableText = string.Empty;
    [ObservableProperty] private string _pointsEarningUnavailableText = string.Empty;

    // No implemented benefit is currently available to redeem points for.
    // Keep a typed catalog seam so real, validated rewards can be added later.
    public IReadOnlyList<RewardDefinition> Rewards { get; } = Array.Empty<RewardDefinition>();

    public PointsPageViewModel() => UpdateLocalizedTexts();

    protected sealed override void UpdateLocalizedTexts()
    {
        PointsTitleText = LanguageHelper.GetText("PointsTitle");
        PointsSubtitleText = LanguageHelper.GetText("PointsSubtitle");
        YourPointsText = LanguageHelper.GetText("YourPoints");
        EarnPointsText = LanguageHelper.GetText("EarnPoints");
        RedeemText = LanguageHelper.GetText("Redeem");
        RewardsText = LanguageHelper.GetText("Rewards");
        RewardsUnavailableText = LanguageHelper.GetText("RewardsUnavailable");
        PointsEarningUnavailableText = LanguageHelper.GetText("PointsEarningUnavailable");
    }
}

public sealed record RewardDefinition(string Id, string Name, string Description, int Cost);
