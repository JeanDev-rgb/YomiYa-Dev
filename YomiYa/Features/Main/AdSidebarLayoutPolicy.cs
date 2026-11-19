namespace YomiYa.Features.Main;

public static class AdSidebarLayoutPolicy
{
    public const double ExpandedWidth = 360;

    public static double GetWidth(bool isReaderOpen, bool isCompactWindow, bool isExpanded) => ExpandedWidth;
}
