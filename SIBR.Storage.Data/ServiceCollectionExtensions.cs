using System;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using NodaTime;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using Serilog.Formatting.Json;

namespace SIBR.Storage.Data
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddSerilog(this IServiceCollection services)
        {
            var logger = new LoggerConfiguration()
                .ConfigureForNodaTime(DateTimeZoneProviders.Tzdb)
                .Enrich.With<ShortSourceContextEnricher>()
                .MinimumLevel.Debug()
                .WriteTo.Async(async =>
                {
                    async.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] [{ShortSourceContext}] {Message:lj}{NewLine}{Exception}", restrictedToMinimumLevel: LogEventLevel.Information);
                    async.File("logs/sibr-.log", LogEventLevel.Debug, rollingInterval: RollingInterval.Day);
                    async.File(new JsonFormatter(), "logs/sibr-.json", LogEventLevel.Debug, rollingInterval: RollingInterval.Day);
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
                .AddSingleton<ObjectStore>()
                .AddSingleton<UpdateStore>()
                .AddSingleton<GameUpdateStore>()
                .AddSingleton<GameStore>()
                .AddSingleton<PlayerUpdateStore>()
                .AddSingleton<SiteUpdateStore>()
                .AddSingleton<IdolsTributesStore>()
                .AddSingleton<TeamUpdateStore>()
                .AddSingleton<IClock>(SystemClock.Instance);
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