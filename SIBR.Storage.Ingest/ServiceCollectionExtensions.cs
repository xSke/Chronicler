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
                .AddSingleton<EventStream>()
                .AddSingleton<StreamDataConsumer>()
                .AddSingleton<IdolsListWorker>()
                .AddSingleton<TeamPlayerDataWorker>()
                .AddSingleton<GlobalEventsWorker>();
        }
    }
}