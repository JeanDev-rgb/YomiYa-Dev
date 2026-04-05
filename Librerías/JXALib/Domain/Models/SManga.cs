using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using YomiYa.Core.Imaging;
using YomiYa.Source.Models;

namespace YomiYa.Domain.Models;

public partial class SManga : ObservableObject
{
    public const int Unknown = 0;
    public const int Ongoing = 1;
    public const int Completed = 2;
    public const int Licensed = 3;
    public const int PublishingFinished = 4;
    public const int Cancelled = 5;
    public const int OnHiatus = 6;

    [ObservableProperty] private bool _isFavorite;
    public string Url { get; set; }
    public string Title { get; set; }
    public string? Artist { get; set; }
    public string? Author { get; set; }
    public string? Description { get; set; }
    public List<string>? Genre { get; set; }
    public int Status { get; set; }
    public string? ThumbnailUrl { get; set; }
    public Bitmap? Cover { get; set; }
    public UpdateStrategy UpdateStrategy { get; set; }
    public bool Initialized { get; set; }
    public string? Plugin { get; set; }

    public async Task LoadCoverAsync()
    {
        if (Cover is null && !string.IsNullOrEmpty(ThumbnailUrl))
        {
            Cover = await ThumbnailUrl.LoadImageAsync(isCover: true);
        }
    }
}