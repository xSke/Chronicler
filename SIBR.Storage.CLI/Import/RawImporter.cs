using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Dapper;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using SIBR.Storage.CLI.Models;
using SIBR.Storage.Data;

namespace SIBR.Storage.CLI.Import
{
    public class RawImporter: FileImporter
    {
        private readonly MessagePackSerializerOptions _msgpackOpts;
        private readonly Database _db;

        public RawImporter(IServiceProvider services) : base(services)
        {
            _msgpackOpts = services.GetRequiredService<MessagePackSerializerOptions>();
            _db = services.GetRequiredService<Database>();
        }

        public override async Task Run(ImportOptions opts)
        {
            await ImportUpdates(opts.Directory);
            await ImportObjects(opts.Directory);
            await ImportSiteUpdates(opts.Directory);
            await ImportBinaryObjects(opts.Directory);
            await ImportGameUpdates(opts.Directory);
        }

        private async Task ImportUpdates(string directory)
        {
            await DoImport<RawUpdate>(
                Path.Join(directory, "updates.dat"), 
                "tmp_updates",
                "source_id uuid, type smallint, timestamp timestamptz, hash uuid, entity_id uuid, update_id uuid",
                "updates", 
                "source_id, type, timestamp, hash, entity_id, update_id", 
                100000,
                (i, obj) => i.WriteRowAsync(default,
                    obj.SourceId,
                    obj.Type,
                    new DateTimeOffset(obj.Timestamp, TimeSpan.Zero),
                    obj.Hash,
                    obj.EntityId,
                    obj.UpdateId));
        }
        
        private async Task ImportObjects(string directory)
        {
            await DoImport<RawObject>(
                Path.Join(directory, "objects.dat"), 
                "tmp_objects",
                "hash uuid, data jsonb",
                "objects", 
                "hash, data", 
                1000,
                (i, obj) => i.WriteRowAsync(default,
                    obj.Hash,
                    obj.Data));
        }
        
        private async Task ImportGameUpdates(string directory)
        {
            await DoImport<RawGameUpdate>(
                Path.Join(directory, "game_updates.dat"), 
                "tmp_game_updates",
                "source_id uuid, timestamp timestamptz, game_id uuid, hash uuid, season smallint, day smallint, tournament smallint",
                "game_updates", 
                "source_id, timestamp, game_id, hash, season, day, tournament", 
                100000,
                (i, obj) => i.WriteRowAsync(default,
                    obj.SourceId,
                    new DateTimeOffset(obj.Timestamp, TimeSpan.Zero),
                    obj.GameId,
                    obj.Hash,
                    obj.Season,
                    obj.Day,
                    obj.Tournament));
        }

        private async Task ImportBinaryObjects(string directory)
        {
            await DoImport<RawObject>(
                Path.Join(directory, "binary_objects.dat"), 
                "tmp_binary_objects",
                "hash uuid, data bytea",
                "binary_objects",
                "hash, data", 
                1000,
                (i, obj) => i.WriteRowAsync(default,
                    obj.Hash,
                    obj.Data));
        }

        private async Task ImportSiteUpdates(string directory)
        {
            await DoImport<RawSiteUpdate>(
                Path.Join(directory, "site_updates.dat"), 
                "tmp_site_updates",
                "source_id uuid, timestamp timestamptz, path text, hash uuid, last_modified timestamptz",
                "site_updates",
                "source_id, timestamp, path, hash, last_modified", 
                1000, async (i, obj) =>
                {
                    await i.StartRowAsync();
                    await i.WriteAsync(obj.SourceId);
                    await i.WriteAsync(new DateTimeOffset(obj.Timestamp, TimeSpan.Zero));
                    await i.WriteAsync(obj.Path);
                    await i.WriteAsync(obj.Hash);
                    if (obj.LastModified != null)
                        await i.WriteAsync(new DateTimeOffset(obj.LastModified.Value, TimeSpan.Zero));
                    else
                        await i.WriteNullAsync();
                });
        }

        private async Task DoImport<T>(string filename, string tmpTableName, string tmpTableColumns, string targetTable,
            string targetTableColumns, int logInterval, Func<NpgsqlBinaryImporter, T, Task> fn)
        {
            await using var conn = await _db.Obtain();
            await conn.ExecuteAsync($"create temporary table {tmpTableName}({tmpTableColumns})");

            await using (var importer = conn.BeginBinaryImport($"copy {tmpTableName} from stdin (format binary)"))
            {
                await foreach (var objs in ReadData<T>(filename, logInterval))
                {
                    foreach (var obj in objs) 
                        await fn(importer, obj);
                }
            }
            
            _logger.Information("Inserting into main table...");
            var rows = await conn.ExecuteAsync(
                $"insert into {targetTable} ({targetTableColumns}) select {targetTableColumns} from {tmpTableName} on conflict do nothing");
            _logger.Information("Inserted {Rows} new rows into {TargetTable}", rows, targetTable);

            await conn.ExecuteAsync($"drop table {tmpTableName}");
        }

        private async IAsyncEnumerable<T[]> ReadData<T>(string filename, int logInterval)
        {
            _logger.Information("Importing from {Filename}", filename);
            await using var stream = File.OpenRead(filename);
            
            using var reader = new MessagePackStreamReader(stream);

            var count = 0;
            while (await reader.ReadAsync(default) is {} array)
            {
                var objects = MessagePackSerializer.Deserialize<T[]>(array, _msgpackOpts);
                
                foreach (var _ in objects)
                    if (++count % logInterval == 0)
                        _logger.Information("Imported {Count} objects so far", count);

                yield return objects;
            }
        }
    }
}