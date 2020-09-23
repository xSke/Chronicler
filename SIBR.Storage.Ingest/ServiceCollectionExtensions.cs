using System.Net.Http;
using Microsoft.Extensions.DependencyInjection;

namespace SIBR.Storage.Ingest
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddSibrIngest(this IServiceCollection services)
        {
            return services
                .AddSingleton(_ =>
                {
                    var client = new HttpClient();
                    client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent",
                        "Immaterial/0.1 (hi TGB! if I'm hitting you too hard, let me (@Ske#6201 @ SIBR discord) know. Hopefully not too bad?)");
                    return client;
                })
                .AddSingleton<EventStream>()
                .AddSingleton<StreamDataWorker>()
                .AddSingleton<TeamPlayerDataWorker>()
                .AddSingleton<SiteUpdateWorker>()
                .AddSingleton<GameEndpointWorker>();
        }
    }
}