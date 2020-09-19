using System;
using System.Threading.Tasks;
using Serilog;

namespace SIBR.Storage.Ingest
{
    public abstract class BaseWorker
    {
        private readonly ILogger _logger;

        protected BaseWorker(ILogger logger)
        {
            _logger = logger;
        }

        protected abstract Task Run();

        public async Task Start()
        {
            while (true)
            {
                try
                {
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