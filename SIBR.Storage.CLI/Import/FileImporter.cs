using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace SIBR.Storage.CLI.Import
{
    public abstract class FileImporter
    {
        protected readonly ILogger _logger;
        
        protected FileImporter(IServiceProvider services)
        {
            _logger = services.GetRequiredService<ILogger>().ForContext(GetType());
        }

        public abstract Task Run(ImportOptions opts);
    }
}