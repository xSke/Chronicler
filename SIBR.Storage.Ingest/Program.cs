using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using SIBR.Storage.Data;

namespace SIBR.Storage.Ingest
{
    class Program
    {
        static void Main(string[] args)
        {
            var services = new ServiceCollection()
                .AddSerilog()
                .AddSibrStorage()
                .AddSibrIngest()
                .BuildServiceProvider();

            services.GetRequiredService<Database>()
                .RunMigrations().GetAwaiter().GetResult();

            RunWorkers(
                services.GetRequiredService<StreamDataConsumer>()
            );
        }

        private static void RunWorkers(params BaseWorker[] workers)
        {
            Task.WhenAll(workers.Select(w => w.Start()))
                .GetAwaiter().GetResult();
        }
    }
}