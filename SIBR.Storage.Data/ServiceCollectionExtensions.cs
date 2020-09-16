using System;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace SIBR.Storage.Data
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddSerilog(this IServiceCollection services)
        {
            var logger = new LoggerConfiguration()
                .WriteTo.Async(async =>
                {
                    async.Console();
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
                .AddSingleton<GameUpdateStore>();
        }
    }
}