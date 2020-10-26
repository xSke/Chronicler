using System;
using System.IO;
using System.IO.Compression;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using NodaTime;
using NodaTime.Serialization.SystemTextJson;
using Npgsql;
using NpgsqlTypes;
using Serilog;
using SIBR.Storage.Data;

namespace SIBR.Storage.CLI.Export
{
    public class RawExport
    {
        private readonly Database _db;
        private readonly JsonSerializerOptions _opts;
        private readonly ILogger _logger;

        public RawExport(Database db, ILogger logger)
        {
            _db = db;
            _logger = logger.ForContext<RawExport>();
            _opts = new JsonSerializerOptions
                {
                    Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                }
                .ConfigureForNodaTime(DateTimeZoneProviders.Tzdb);
        }

        public async Task Run(Program.ExportRawCmd opts)
        {
            try
            {
                await ExportAllUpdates(Path.Join(opts.Directory, "updates.json.gz"));
                await ExportAllObjects(Path.Join(opts.Directory, "objects.json.gz"));
                await ExportAllBinaryObjects(Path.Join(opts.Directory, "binary_objects.json.gz"));
                await ExportAllGameUpdates(Path.Join(opts.Directory, "game_updates.json.gz"));
                await ExportAllSiteUpdates(Path.Join(opts.Directory, "site_updates.json.gz"));
            }
            catch (Exception e)
            {
                _logger.Error(e, "Error exporting");
            }
        }

        private async Task ExportAllSiteUpdates(string filename)
        {
            await WriteToFile(filename, "site_updates", reader =>
            {
                var su = new RawSiteUpdate
                {
                    Timestamp = reader.Read<Instant>(NpgsqlDbType.TimestampTz),
                    Path = reader.Read<string>(NpgsqlDbType.Text),
                    Hash = reader.Read<Guid>(NpgsqlDbType.Uuid)
                };
                
                // Won't be null in prod but my local test doesn't have not null constraint sooo~
                if (!reader.IsNull) su.SourceId = reader.Read<Guid>(NpgsqlDbType.Uuid);
                else reader.Skip();
                
                if (!reader.IsNull) su.LastModified = reader.Read<Instant>(NpgsqlDbType.TimestampTz);
                else reader.Skip();

                return su;
            });
        }

        private async Task ExportAllBinaryObjects(string filename)
        {
            await WriteToFile(filename, "binary_objects", reader => new RawBinaryObject
            {
                Hash = reader.Read<Guid>(NpgsqlDbType.Uuid),
                Data = Convert.ToBase64String(reader.Read<byte[]>(NpgsqlDbType.Bytea))
            });
        }

        private async Task ExportAllGameUpdates(string filename)
        {
            await WriteToFile(filename, "game_updates", reader => new RawGameUpdate
            {
                Timestamp = reader.Read<Instant>(NpgsqlDbType.TimestampTz),
                GameId = reader.Read<Guid>(NpgsqlDbType.Uuid),
                Hash = reader.Read<Guid>(NpgsqlDbType.Uuid),
                SourceId = reader.Read<Guid>(NpgsqlDbType.Uuid),
                Season = reader.Read<short>(NpgsqlDbType.Smallint),
                Day = reader.Read<short>(NpgsqlDbType.Smallint),
            });
        }

        private async Task ExportAllObjects(string filename)
        {
            JsonDocument doc = null;
            
            await WriteToFile(filename, "objects", reader =>
            {
                var hash = reader.Read<Guid>(NpgsqlDbType.Uuid);
                var json = reader.Read<byte[]>(NpgsqlDbType.Bytea);

                doc?.Dispose();
                
                // For some reason it starts the returned string with a single 0x01 byte so we gotta trim that off
                var jr = new Utf8JsonReader(json.AsSpan(1));
                doc = JsonDocument.ParseValue(ref jr);

                return new RawObject
                {
                    Hash = hash,
                    Data = doc.RootElement,
                };
            });
            
            doc?.Dispose();
        }

        private async Task ExportAllUpdates(string filename)
        {
            await WriteToFile(filename, "updates", reader => new RawUpdate
            {
                Type = reader.Read<short>(NpgsqlDbType.Smallint),
                Timestamp = reader.Read<Instant>(NpgsqlDbType.TimestampTz),
                Hash = reader.Read<Guid>(NpgsqlDbType.Uuid),
                EntityId = reader.Read<Guid>(NpgsqlDbType.Uuid),
                SourceId = reader.Read<Guid>(NpgsqlDbType.Uuid),
                UpdateId = reader.Read<Guid>(NpgsqlDbType.Uuid),
            });
        }

        private async Task WriteToFile<T>(string filename, string table, Func<NpgsqlBinaryExporter, T> mapper)
        {
            _logger.Information("Exporting from {Table} to {Filename}...", table, filename);
            
            await using var conn = await _db.Obtain();
            await using var reader = conn.BeginBinaryExport($"copy {table} to stdout (format binary)");

            await using var file = File.Open(filename, FileMode.Create, FileAccess.Write);
            await using var gz = new GZipStream(file, CompressionLevel.Optimal);

            var rows = 0;
            while (await reader.StartRowAsync() > -1)
            {
                if (rows % 10000 == 0) 
                    _logger.Information("Writing to {Filename} ({Rows} so far)", filename, rows);

                await using (var writer = new Utf8JsonWriter(gz))
                    JsonSerializer.Serialize(writer, mapper(reader), _opts);
                gz.WriteByte(0x0a);

                rows++;
            }
            
            _logger.Information("Done exporting {Table}.", table);
        }

        private struct RawUpdate
        {
            [JsonPropertyName("ty")] public short Type { get; set; }
            [JsonPropertyName("t")] public Instant Timestamp { get; set; }
            [JsonPropertyName("h")] public Guid Hash { get; set; }
            [JsonPropertyName("e")] public Guid EntityId { get; set; }
            [JsonPropertyName("s")] public Guid SourceId { get; set; }
            [JsonPropertyName("id")] public Guid UpdateId { get; set; }
        }

        private struct RawObject
        {
            [JsonPropertyName("h")] public Guid Hash { get; set; }
            [JsonPropertyName("d")] public JsonElement Data { get; set; }
        }

        private struct RawBinaryObject
        {
            [JsonPropertyName("h")] public Guid Hash { get; set; }
            [JsonPropertyName("d")] public string Data { get; set; }
        }

        private struct RawGameUpdate
        {
            [JsonPropertyName("t")] public Instant Timestamp { get; set; }
            [JsonPropertyName("g")] public Guid GameId { get; set; }
            [JsonPropertyName("h")] public Guid Hash { get; set; }
            [JsonPropertyName("s")] public Guid SourceId { get; set; }
            [JsonPropertyName("gs")] public short Season { get; set; }
            [JsonPropertyName("gd")] public short Day { get; set; }
        }

        private struct RawSiteUpdate
        {
            [JsonPropertyName("t")] public Instant Timestamp { get; set; }
            [JsonPropertyName("p")] public string Path { get; set; }
            [JsonPropertyName("h")] public Guid Hash { get; set; }
            [JsonPropertyName("s")] public Guid SourceId { get; set; }
            [JsonPropertyName("lm")] public Instant? LastModified { get; set; }
        }
    }
}