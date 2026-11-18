using System.Text.Json;
using YomiYa.Core.Settings;
using YomiYa.Features.Main;

namespace YomiYa.Tests;

public class AdSidebarLayoutTests
{
    [Theory]
    [InlineData(900, true)]
    [InlineData(1119, true)]
    [InlineData(1120, false)]
    [InlineData(1400, false)]
    public void Compact_layout_is_selected_from_window_width(double width, bool expected)
    {
        Assert.Equal(expected, AdSidebarLayoutPolicy.IsCompact(width));
    }

    [Theory]
    [InlineData(false, false, true, 250)]
    [InlineData(false, false, false, 48)]
    [InlineData(false, true, true, 0)]
    [InlineData(true, false, true, 0)]
    public void Reader_and_compact_window_hide_sidebar_without_changing_preference(
        bool readerOpen, bool compact, bool preferredExpanded, double expectedWidth)
    {
        Assert.Equal(expectedWidth, AdSidebarLayoutPolicy.GetWidth(readerOpen, compact, preferredExpanded));
        Assert.Equal(preferredExpanded, JsonSerializer.Deserialize<AppSettings>(
            JsonSerializer.Serialize(new AppSettings { IsAdSidebarExpanded = preferredExpanded }))!.IsAdSidebarExpanded);
    }

    [Fact]
    public void Existing_settings_without_sidebar_preference_default_to_expanded()
    {
        var settings = JsonSerializer.Deserialize<AppSettings>("{}");

        Assert.True(settings!.IsAdSidebarExpanded);
    }
}
