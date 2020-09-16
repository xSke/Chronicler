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
        
        static async Task Main(string[] args)
        {
            var services = new ServiceCollection()
                .AddSerilog()
                .AddSibrStorage()
                .AddSingleton<HttpClient>()
                .AddSingleton<EventStream>()
                .AddSingleton<StreamDataConsumer>()
                .AddSingleton<GameLogsImporter>()
                .BuildServiceProvider();

            var result = Parser.Default.ParseArguments<ImportLogsOptions, object>(args);
            await result.WithParsedAsync<ImportLogsOptions>(async opts =>
            {
                await services.GetRequiredService<GameLogsImporter>().Import(opts.Directory);
            });

            // services.GetRequiredService<Database>()
            //     .RunMigrations().GetAwaiter().GetResult();
        }
    }
}