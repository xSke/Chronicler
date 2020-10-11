using System;
using System.Linq;
using System.Threading.Tasks;
using CommandLine;
using Microsoft.Extensions.DependencyInjection;
using NodaTime;
using SIBR.Storage.CLI.Export;
using SIBR.Storage.CLI.Import;
using SIBR.Storage.Data;
using SIBR.Storage.Data.Models;
using SIBR.Storage.Ingest;

namespace SIBR.Storage.CLI
{
    public class Program
    {
        [Verb("import")]
        public class ImportCmd
        {
            [Value(0, MetaName = "type")] public string Type { get; set; }

            [Value(1, MetaName = "sourceid")] public Guid SourceId { get; set; }

            [Value(2, MetaName = "directory", HelpText = "The directory to read log files from")]
            public string Directory { get; set; }
        }

        [Verb("migrations")]
        public class MigrationsCmd
        {
            [Option("repair")]
            public bool Repair { get; set; }
        }

        [Verb("ingest")]
        public class IngestCmd
        {
            [Value(0, MetaName = "sourceid")] public Guid SourceId { get; set; }
        }

        [Verb("export", HelpText = "Export data to files")]
        public class ExportCmd
        {
            [Value(0, MetaName = "directory", HelpText = "Output directory")]
            public string Directory { get; set; }

            [Option("compress")] public bool Compress { get; set; }
        }
        
        [Verb("exportdb", HelpText = "Export data to SQLite")]
        public class ExportDbCmd
        {
            [Value(0, MetaName = "file", HelpText = "Output file")]
            public string File { get; set; }
        }

        [Verb("replay")]
        public class ReplayCmd
        {
            [Option("type")]
            public UpdateType? Type { get; set; }
            
            [Option("after")]
            public DateTimeOffset? After { get; set; }
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
                .AddSingleton<FlatFileExport>()
                .AddSingleton<StreamReplay>()
                .AddSingleton<SQLiteExport>()
                .BuildServiceProvider();

            var result = Parser.Default
                .ParseArguments<ImportCmd, MigrationsCmd, IngestCmd, ExportCmd, ExportDbCmd, ReplayCmd>(args);

            if (result.TypeInfo.Current != typeof(MigrationsCmd))
                // Init sets up NodaTime in a way that breaks Evolve, so don't do it if we're migrating
                Database.Init();

            await result.WithParsedAsync<ImportCmd>(opts => HandleImport(services, opts));
            await result.WithParsedAsync<MigrationsCmd>(opts => HandleMigrations(services, opts));
            await result.WithParsedAsync<IngestCmd>(opts => HandleIngest(services, opts));
            await result.WithParsedAsync<ReplayCmd>(opts => HandleReplay(services, opts));
            await result.WithParsedAsync<ExportCmd>(opts => HandleExport(services, opts));
            await result.WithParsedAsync<ExportDbCmd>(opts => HandleExportDb(services, opts));
        }

        private static Task HandleExportDb(IServiceProvider services, ExportDbCmd opts)
        {
            return services.GetRequiredService<SQLiteExport>().Run(opts);
        }

        private static Task HandleMigrations(IServiceProvider services, MigrationsCmd opts)
        {
            return services.GetRequiredService<Database>().RunMigrations(opts.Repair);
        }

        private static async Task HandleIngest(IServiceProvider services, IngestCmd opts)
        {
            await Task.WhenAll(IngestWorkers.CreateWorkers(services, opts.SourceId)
                .Select(w => w.Start()));
        }

        private static async Task HandleExport(IServiceProvider services, ExportCmd opts)
        {
            await services.GetRequiredService<FlatFileExport>().Run(new FlatFileExport.ExportOptions
            {
                OutDir = opts.Directory,
                Compress = opts.Compress
            });
        }

        private static async Task HandleReplay(IServiceProvider services, ReplayCmd opts)
        {
            await services.GetRequiredService<StreamReplay>().Run(new StreamReplay.ReplayOptions
            {
                Type = opts.Type,
                After = opts.After != null ? Instant.FromDateTimeOffset(opts.After.Value) : (Instant?) null
            });
        }

        private static async Task HandleImport(IServiceProvider services, ImportCmd opts)
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
}