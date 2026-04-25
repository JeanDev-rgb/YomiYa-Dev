using YomiYa.Core.IPC;

namespace YomiYa.Extensions.Es // Cambiar namespace según plugin
{
    class Program
    {
        static async Task Main(string[] args)
        {
            var scraper = new NovelCool(); // Cambiar por la clase de tu plugin (ej. new Akaya())
            int port = args.Length > 0 && int.TryParse(args[0], out int p) ? p : 50000;
            
            await TcpPluginRunner.RunAsync(scraper, port);
        }
    }
}