using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace SIBR.Storage.Ingest
{
    public abstract class BaseWorker
    {
        protected readonly ILogger _logger;

        protected BaseWorker(IServiceProvider services)
        {
            _logger = services.GetRequiredService<ILogger>()
                .ForContext(GetType());
        }

        protected abstract Task Run();

        public async Task Start()
        {
            while (true)
            {
                try
                {
                    _logger.Information("Starting ingest worker {WorkerType}", GetType().Name);
                    await Run();
                }
                catch (Exception e)
                {
                    _logger.Error(e, "Error while running worker {WorkerType}", GetType().Name);
                }
            }
        }
    }
}