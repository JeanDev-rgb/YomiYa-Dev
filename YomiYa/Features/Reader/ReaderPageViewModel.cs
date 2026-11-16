using System;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using YomiYa.Domain.Models;

namespace YomiYa.Features.Reader;

public partial class ReaderPageViewModel : ObservableObject, IDisposable
{
    [ObservableProperty] private Bitmap? _data;
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private bool _hasError;

    public ReaderPageViewModel(Page page)
    {
        Index = page.Index;
        Url = page.Url;
        ImageUrl = page.ImageUrl;
        Uri = page.Uri;
        Data = page.Data;
        IsLoading = Data is null && !string.IsNullOrWhiteSpace(ImageUrl);
    }

    public int Index { get; }
    public string Url { get; }
    public string? ImageUrl { get; }
    public Uri? Uri { get; }

    public void BeginLoading()
    {
        HasError = false;
        IsLoading = true;
    }

    public void CompleteLoading(Bitmap? bitmap)
    {
        var previous = Data;
        Data = bitmap;
        IsLoading = false;
        HasError = bitmap is null;
        if (previous is not null && !ReferenceEquals(previous, bitmap)) previous.Dispose();
    }

    public void Dispose()
    {
        var bitmap = Data;
        Data = null;
        IsLoading = false;
        bitmap?.Dispose();
    }
}
