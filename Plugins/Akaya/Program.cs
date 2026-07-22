using YomiYa.Core.IPC;

namespace YomiYa.Extensions.Es;

public static class Program
{
    private static async Task Main(string[] args)
    {
        var scraper = new Akaya();
        var port = args.Length > 0 && int.TryParse(args[0], out var p) ? p : 50000;

        await TcpPluginRunner.RunAsync(scraper, port);
    }
}