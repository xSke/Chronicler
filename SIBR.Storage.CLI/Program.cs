using System;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using CommandLine;
using Microsoft.Extensions.DependencyInjection;
using SIBR.Storage.CLI.Import;
using SIBR.Storage.Data;
using SIBR.Storage.Ingest;

namespace SIBR.Storage.CLI
{
    class Program
    {
        [Verb("import")]
        public class ImportOptions
        {
            [Value(0, MetaName = "type")]
            public string Type { get; set; }
            
            [Value(1, MetaName = "sourceid")]
            public Guid SourceId { get; set; }

            [Value(2, MetaName = "directory", HelpText = "The directory to read log files from")]
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
                .AddSingleton<IdolLogsImporter>()
                .BuildServiceProvider();

            var result = Parser.Default
                .ParseArguments<ImportOptions, Migrations, Ingest>(args);
            
            if (result.TypeInfo.Current != typeof(Migrations))
                // Init sets up NodaTime in a way that breaks Evolve, so don't do it if we're migrating
                Database.Init();

            await result.WithParsedAsync<ImportOptions>(opts => RunImport(services, opts));

            await result.WithParsedAsync<Migrations>(_ =>
                services.GetRequiredService<Database>().RunMigrations());

            await result.WithParsedAsync<Ingest>(async opts =>
            {
                await Task.WhenAll(IngestWorkers.CreateWorkers(services, DataSources.ImmaterialAstridLocal)
                    .Select(w => w.Start()));
            });
        }

        private static async Task RunImport(ServiceProvider services, ImportOptions opts)
        {
            S3FileImporter importer = opts.Type switch
            {
                "hourly" => new HourlyLogsImporter(services, opts.SourceId),
                "gamelogs" => new GameLogsImporter(services, opts.SourceId),
                "idols" => new IdolLogsImporter(services, opts.SourceId),
                "mongotributes" => new MongodbTributesImporter(services, opts.SourceId),
                _ => throw new ArgumentException($"Unknown import type {opts.Type}")
            };
            await importer.Run(new S3ImportOptions
            {
                Directory = opts.Directory
            });
        }
    }
};