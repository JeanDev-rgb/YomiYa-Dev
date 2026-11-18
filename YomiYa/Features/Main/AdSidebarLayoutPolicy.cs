namespace YomiYa.Features.Main;

public static class AdSidebarLayoutPolicy
{
    public const double MinimumWindowWidth = 1120;
    public const double ExpandedWidth = 250;
    public const double CollapsedWidth = 48;

    public static bool IsCompact(double windowWidth) =>
        windowWidth > 0 && windowWidth < MinimumWindowWidth;

    public static double GetWidth(bool isReaderOpen, bool isCompactWindow, bool isExpanded) =>
        isReaderOpen || isCompactWindow
            ? 0
            : isExpanded ? ExpandedWidth : CollapsedWidth;
}
