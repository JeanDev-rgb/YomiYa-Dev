using YomiYa.Features.Points;

namespace YomiYa.Tests;

public class RewardCatalogTests
{
    [Fact]
    public void Catalog_does_not_offer_unimplemented_or_chargeable_rewards()
    {
        var viewModel = new PointsPageViewModel();

        Assert.Empty(viewModel.Rewards);
        Assert.Equal(0, viewModel.CurrentPoints);
        Assert.Equal(0, viewModel.TotalEarnedPoints);
    }
}
