using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using CommandLine;
using Microsoft.Extensions.DependencyInjection;
using SIBR.Storage.Data;
using SIBR.Storage.Ingest;

namespace SIBR.Storage.CLI
{
    class Program
    {
        [Verb("import-logs")]
        public class ImportLogsOptions
        {
            [Value(0, MetaName="directory", HelpText = "The directory to read log files from")]
            public string Directory { get; set; }
        }
        
        [Verb("import-hourly")]
        public class ImportHourlyOptions
        {
            [Value(0, MetaName="directory", HelpText = "The directory to read hourly log files from")]
            public string Directory { get; set; }
        }

        [Verb("migrations")]
        public class Migrations
        {
            
        }

        [Verb("ingest")]
        public class Ingest
        {
            
        }
        
        static async Task Main(string[] args)
        {
            var services = new ServiceCollection()
                .AddSerilog()
                .AddSibrStorage()
                .AddSibrIngest()
                .AddSingleton<GameLogsImporter>()
                .AddSingleton<HourlyLogsImporter>()
                .BuildServiceProvider();

            var result = Parser.Default.ParseArguments<ImportLogsOptions, ImportHourlyOptions, Migrations, Ingest>(args);
            await result.WithParsedAsync<ImportLogsOptions>(opts => 
                services.GetRequiredService<GameLogsImporter>().Import(opts.Directory));
            await result.WithParsedAsync<ImportHourlyOptions>(opts => 
                services.GetRequiredService<HourlyLogsImporter>().Import(opts.Directory));
            await result.WithParsedAsync<Migrations>(_ =>
                services.GetRequiredService<Database>().RunMigrations());
            
            await result.WithParsedAsync<Ingest>(async opts =>
            {
                var workers = new BaseWorker[]
                {
                    services.GetRequiredService<GlobalEventsWorker>(),
                    services.GetRequiredService<IdolsListWorker>(),
                    services.GetRequiredService<StreamDataConsumer>(),
                    services.GetRequiredService<TeamPlayerDataWorker>()
                };

                await Task.WhenAll(workers.Select(w => w.Start()));
            });
        }
    }
};