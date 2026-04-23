using System.Diagnostics;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using FluentAvalonia.UI.Controls;
using YomiYa.Features.Main;

namespace YomiYa.Core.Dialogs;

public interface IDialogService
{
    Task ShowRestartDialogAsync();
}

public class DialogService : IDialogService
{
    public async Task ShowRestartDialogAsync()
    {
        var dialog = new FAContentDialog
        {
            Title = "Restart Required",
            Content = "The application needs to be restarted for the changes to take effect.",
            PrimaryButtonText = "Restart Now",
            CloseButtonText = "Later"
        };

        if (MainWindow.Instance != null)
        {
            var result = await dialog.ShowAsync(MainWindow.Instance);
            if (result == FAContentDialogResult.Primary)
                if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
                {
                    var process = new Process
                    {
                        StartInfo = new ProcessStartInfo
                        {
                            FileName = Process.GetCurrentProcess().MainModule?.FileName,
                            UseShellExecute = true
                        }
                    };
                    process.Start();
                    desktop.Shutdown();
                }
        }
    }
}