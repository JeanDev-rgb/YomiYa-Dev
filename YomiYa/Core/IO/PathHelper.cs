using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using YomiYa.Features.Main;

namespace YomiYa.Core.IO;

public static class PathHelper
{
    public static string PluginsPath => System.IO.Path.Combine(AppContext.BaseDirectory, "Plugins");

    public static async Task<List<string>?> SelectPath(string title, bool allowMultiple = false)
    {
        var topLevel = TopLevel.GetTopLevel(MainWindow.Instance);

        var options = new FilePickerOpenOptions
        {
            Title = title,
            AllowMultiple = allowMultiple
        };

        var files = await topLevel!.StorageProvider.OpenFilePickerAsync(options);

        return files.Any() ? files.Select(file => file.Path.LocalPath).ToList() : null;
    }

    public static string GetDatabasePath()
    {
        var dbDirectory = "Data";
        var dbName = "yomiya_library.db";
        return Path.Combine(AppContext.BaseDirectory, dbDirectory, dbName);
    }
}