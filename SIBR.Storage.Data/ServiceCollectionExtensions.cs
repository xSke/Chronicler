using System;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using Serilog.Core;
using Serilog.Events;

namespace SIBR.Storage.Data
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddSerilog(this IServiceCollection services)
        {
            var logger = new LoggerConfiguration()
                .Enrich.With<ShortSourceContextEnricher>()
                .WriteTo.Async(async =>
                {
                    async.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] [{ShortSourceContext,22}] {Message:lj}{NewLine}{Exception}");
                })
                .CreateLogger();

            Log.Logger = logger;

            return services.AddSingleton<ILogger>(logger);
        }
        
        public static IServiceCollection AddSibrStorage(this IServiceCollection services)
        {
            var connectionString = Environment.GetEnvironmentVariable("POSTGRES_CONNECTION");
            if (connectionString == null)
                throw new ArgumentException("Needs POSTGRES_CONNECTION in environment variable!");
            
            return services
                .AddSingleton(svc => new Database(svc, connectionString))
                .AddSingleton<StreamUpdateStore>()
                .AddSingleton<GameUpdateStore>()
                .AddSingleton<TeamUpdateStore>()
                .AddSingleton<PlayerUpdateStore>()
                .AddSingleton<SiteUpdateStore>()
                .AddSingleton<MiscStore>();
        }
        
        private class ShortSourceContextEnricher: ILogEventEnricher
        {
            public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
            {
                if (!(logEvent.Properties["SourceContext"] is ScalarValue sourceContext))
                    return;

                var shortSourceContext = ((string) sourceContext.Value).Split(".").LastOrDefault();
                logEvent.AddOrUpdateProperty(propertyFactory.CreateProperty("ShortSourceContext", shortSourceContext));
            }
        }
    }
}