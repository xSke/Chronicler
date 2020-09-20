using System.Net.Http;
using Microsoft.Extensions.DependencyInjection;

namespace SIBR.Storage.Ingest
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddSibrIngest(this IServiceCollection services)
        {
            return services
                .AddSingleton<HttpClient>()
                .AddTransient<EventStream>()
                .AddTransient<StreamDataWorker>()
                .AddTransient<TeamPlayerDataWorker>()
                .AddTransient<SiteUpdateWorker>();
        }
    }
}