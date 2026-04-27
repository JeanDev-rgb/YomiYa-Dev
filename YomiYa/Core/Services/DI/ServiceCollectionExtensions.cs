using Microsoft.Extensions.DependencyInjection;
using YomiYa.Features.Main;

namespace YomiYa.Core.Services.DI;

public static class ServiceCollectionExtensions
{
    public static void AddCommonServices(this IServiceCollection collection)
    {
        collection.AddTransient<MainWindowViewModel>();
    }
}
