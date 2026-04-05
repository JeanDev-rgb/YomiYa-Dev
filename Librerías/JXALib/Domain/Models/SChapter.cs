using CommunityToolkit.Mvvm.ComponentModel;

namespace YomiYa.Domain.Models;

public partial class SChapter : ObservableObject
{
    public string Url { get; set; }
    public string Name { get; set; }
    public long DateUpload { get; set; }
    public float ChapterNumber { get; set; }
    public string? Scanlator { get; set; }

    [ObservableProperty] private bool _isRead;
    [ObservableProperty] private int _lastPageRead;
}