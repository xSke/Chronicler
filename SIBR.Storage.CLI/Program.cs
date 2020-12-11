using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommandLine;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using NodaTime;
using SIBR.Storage.CLI.Export;
using SIBR.Storage.CLI.Import;
using SIBR.Storage.CLI.Utils;
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
            [Option("source")] public Guid? SourceId { get; set; }

            [Value(0, MetaName = "type")] public string Type { get; set; }
            
            [Value(1, MetaName = "directory", HelpText = "The directory to read log files from")]
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
        
        [Verb("exportraw", HelpText = "Export data in raw format")]
        public class ExportRawCmd
        {
            [Value(0, MetaName = "directory", HelpText = "Output directory")]
            public string Directory { get; set; }
        }

        [Verb("replay")]
        public class ReplayCmd
        {
            [Option("type")]
            public UpdateType? Type { get; set; }
            
            [Option("start")]
            public DateTimeOffset? Start { get; set; }
            
            [Option("end")]
            public DateTimeOffset? End { get; set; }
        }

        static async Task Main(string[] args)
        {
            var config = new ConfigurationBuilder()
                .AddJsonFile(cfg =>
                {
                    cfg.FileProvider = new PhysicalFileProvider(Directory.GetCurrentDirectory());
                    cfg.Path = "config.json";
                    cfg.Optional = false;
                })
                .AddEnvironmentVariables()
                .Build()
                .Get<ChronConfiguration>();
            
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
                .AddSingleton<RawExport>()
                .AddMessagePackSettings()
                .BuildServiceProvider();

            var result = Parser.Default
                .ParseArguments<ImportCmd, MigrationsCmd, IngestCmd, ExportCmd, ExportDbCmd, ExportRawCmd, ReplayCmd>(args);

            if (result.TypeInfo.Current != typeof(MigrationsCmd))
                // Init sets up NodaTime in a way that breaks Evolve, so don't do it if we're migrating
                Database.Init();

            await result.WithParsedAsync<ImportCmd>(opts => HandleImport(services, opts));
            await result.WithParsedAsync<MigrationsCmd>(opts => HandleMigrations(services, opts));
            await result.WithParsedAsync<IngestCmd>(opts => HandleIngest(services, opts, config));
            await result.WithParsedAsync<ReplayCmd>(opts => HandleReplay(services, opts));
            await result.WithParsedAsync<ExportCmd>(opts => HandleExport(services, opts));
            await result.WithParsedAsync<ExportDbCmd>(opts => HandleExportDb(services, opts));
            await result.WithParsedAsync<ExportRawCmd>(opts => HandleExportRaw(services, opts));
        }

        private static Task HandleExportRaw(ServiceProvider services, ExportRawCmd opts)
        {
            return services.GetRequiredService<RawExport>().Run(opts);
        }

        private static Task HandleExportDb(IServiceProvider services, ExportDbCmd opts)
        {
            return services.GetRequiredService<SQLiteExport>().Run(opts);
        }

        private static Task HandleMigrations(IServiceProvider services, MigrationsCmd opts)
        {
            return services.GetRequiredService<Database>().RunMigrations(opts.Repair);
        }

        private static async Task HandleIngest(IServiceProvider services, IngestCmd _, ChronConfiguration config)
        {
            var workers = IngestWorkers.CreateWorkers(services, config.Ingest);
            await Task.WhenAll(workers.Select(w => w.Start()));
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
                Start = opts.Start != null ? Instant.FromDateTimeOffset(opts.Start.Value) : (Instant?) null,
                End = opts.End != null ? Instant.FromDateTimeOffset(opts.End.Value) : (Instant?) null
            });
        }

        private static async Task HandleImport(IServiceProvider services, ImportCmd opts)
        {
            FileImporter importer = opts.Type switch
            {
                "hourly" => new HourlyLogsImporter(services, opts.SourceId ?? throw new ArgumentException("Source ID is required")),
                "gamelogs" => new GameLogsImporter(services, opts.SourceId ?? throw new ArgumentException("Source ID is required")),
                "idols" => new IdolLogsImporter(services, opts.SourceId ?? throw new ArgumentException("Source ID is required")),
                "mongotributes" => new MongodbTributesImporter(services, opts.SourceId ?? throw new ArgumentException("Source ID is required")),
                "raw" => new RawImporter(services),
                _ => throw new ArgumentException($"Unknown import type {opts.Type}")
            };
            
            await importer.Run(new ImportOptions
            {
                Directory = opts.Directory
            });
        }
    }
}