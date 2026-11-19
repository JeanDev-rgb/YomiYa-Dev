using YomiYa.Features.Main;

namespace YomiYa.Tests;

public class AdSidebarLayoutTests
{
    [Theory]
    [InlineData(false, false, true)]
    [InlineData(false, false, false)]
    [InlineData(false, true, true)]
    [InlineData(true, false, true)]
    public void Sidebar_stays_visible_during_reading_small_windows_and_manual_collapse(
        bool readerOpen, bool compact, bool preferredExpanded)
    {
        Assert.Equal(AdSidebarLayoutPolicy.ExpandedWidth,
            AdSidebarLayoutPolicy.GetWidth(readerOpen, compact, preferredExpanded));
    }
}
