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
        private interface IImportOptions
        {
            public string Directory { get; set; }
        }
        
        [Verb("import-logs")]
        public class ImportLogsOptions: IImportOptions
        {
            [Value(0, MetaName = "directory", HelpText = "The directory to read log files from")]
            public string Directory { get; set; }
        }

        [Verb("import-hourly")]
        public class ImportHourlyOptions: IImportOptions
        {
            [Value(0, MetaName = "directory", HelpText = "The directory to read hourly log files from")]
            public string Directory { get; set; }
        }

        [Verb("import-idols")]
        public class ImportIdolsOptions: IImportOptions
        {
            [Value(0, MetaName = "directory", HelpText = "The directory to read idol log files from")]
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
                .ParseArguments<ImportLogsOptions, ImportHourlyOptions, ImportIdolsOptions, Migrations, Ingest>(args);

            await result.WithParsedAsync<ImportLogsOptions>(opts =>
                RunS3(new GameLogsImporter(services, DataSources.IlianaS3), opts));

            await result.WithParsedAsync<ImportHourlyOptions>(opts =>
                RunS3(new HourlyLogsImporter(services, DataSources.IlianaS3), opts));

            await result.WithParsedAsync<ImportIdolsOptions>(opts =>
                RunS3(new IdolLogsImporter(services, DataSources.IlianaS3), opts));

            await result.WithParsedAsync<Migrations>(_ =>
                services.GetRequiredService<Database>().RunMigrations());

            await result.WithParsedAsync<Ingest>(async opts =>
            {
                await Task.WhenAll(IngestWorkers.CreateWorkers(services, DataSources.ImmaterialAstridLocal)
                    .Select(w => w.Start()));
            });
        }

        static Task RunS3(S3FileImporter importer, IImportOptions opts)
        {
            return importer.Run(new S3ImportOptions
            {
                Directory = opts.Directory
            });
        }
    }
};