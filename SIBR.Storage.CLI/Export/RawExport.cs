﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using MessagePack;
using MessagePack.Formatters;
using Newtonsoft.Json;
using Npgsql;
using NpgsqlTypes;
using Serilog;
using SIBR.Storage.CLI.Models;
using SIBR.Storage.Data;
using ZstdNet;

namespace SIBR.Storage.CLI.Export
{
    public class RawExport
    {
        private readonly Database _db;
        private readonly MessagePackSerializerOptions _opts;
        private readonly ILogger _logger;

        public RawExport(Database db, ILogger logger, MessagePackSerializerOptions opts)
        {
            _db = db;
            _logger = logger.ForContext<RawExport>();
            _opts = opts;
        }

        public async Task Run(Program.ExportRawCmd opts)
        {
            try
            {
                await ExportAllUpdates(Path.Join(opts.Directory, "updates.ndjson"));
                await ExportAllObjects(Path.Join(opts.Directory, "objects.ndjson"));
                await ExportAllBinaryObjects(Path.Join(opts.Directory, "binary_objects.ndjson"));
                await ExportAllGameUpdates(Path.Join(opts.Directory, "game_updates.ndjson"));
                await ExportAllSiteUpdates(Path.Join(opts.Directory, "site_updates.ndjson"));
                await ExportAllFeed(Path.Join(opts.Directory, "feed.ndjson"));
                await ExportAllPusher(Path.Join(opts.Directory, "pusher.ndjson"));
            }
            catch (Exception e)
            {
                _logger.Error(e.GetBaseException(), "Error exporting");
            }
        }

        private async Task ExportAllSiteUpdates(string filename)
        {
            await WriteToFile(filename, "site_updates(timestamp, path, hash, source_id, last_modified)", reader =>
            {
                var su = new RawSiteUpdate
                {
                    Timestamp = reader.Read<DateTimeOffset>(NpgsqlDbType.TimestampTz).UtcDateTime,
                    Path = reader.Read<string>(NpgsqlDbType.Text),
                    Hash = reader.Read<Guid>(NpgsqlDbType.Uuid)
                };
                
                // Won't be null in prod but my local test doesn't have not null constraint sooo~
                if (!reader.IsNull) su.SourceId = reader.Read<Guid>(NpgsqlDbType.Uuid);
                else reader.Skip();
                
                if (!reader.IsNull) su.LastModified = reader.Read<DateTimeOffset>(NpgsqlDbType.TimestampTz).UtcDateTime;
                else reader.Skip();

                return su;
            }, 25000);
        }

        private async Task ExportAllBinaryObjects(string filename)
        {
            await WriteToFile(filename, "binary_objects(hash, data)", reader => new RawBinaryObject
            {
                Hash = reader.Read<Guid>(NpgsqlDbType.Uuid),
                Data = Convert.ToBase64String(reader.Read<byte[]>(NpgsqlDbType.Bytea))
            }, 1);
        }

        private async Task ExportAllGameUpdates(string filename)
        {
            await WriteToFile(filename, "game_updates(timestamp, game_id, hash, source_id, season, day, tournament)", reader => new RawGameUpdate
            {
                Timestamp = reader.Read<DateTimeOffset>(NpgsqlDbType.TimestampTz).UtcDateTime,
                GameId = reader.Read<Guid>(NpgsqlDbType.Uuid),
                Hash = reader.Read<Guid>(NpgsqlDbType.Uuid),
                SourceId = reader.Read<Guid>(NpgsqlDbType.Uuid),
                Season = reader.Read<short>(NpgsqlDbType.Smallint),
                Day = reader.Read<short>(NpgsqlDbType.Smallint),
                Tournament = reader.Read<short>(NpgsqlDbType.Smallint)
            }, 250000);
        }

        private async Task ExportAllObjects(string filename)
        {
            // JsonDocument doc = null;
            
            await WriteToFile(filename, "objects(hash, data)", reader =>
            {
                var hash = reader.Read<Guid>(NpgsqlDbType.Uuid);
                var json = reader.Read<byte[]>(NpgsqlDbType.Bytea);

                // doc?.Dispose();
                
                // var jr = new Utf8JsonReader(json.AsSpan(1));
                // doc = JsonDocument.ParseValue(ref jr);

                // For some reason it starts the returned string with a single 0x01 byte so we gotta trim that off
                return new RawObject
                {
                    Hash = hash,
                    Data = JsonToString(json)
                };
            }, 2500);
            
            // doc?.Dispose();
        }

        private async Task ExportAllUpdates(string filename)
        {
            await WriteToFile(filename, "updates(type, timestamp, hash, entity_id, source_id, update_id)", reader => new RawUpdate
            {
                Type = reader.Read<short>(NpgsqlDbType.Smallint),
                Timestamp = reader.Read<DateTimeOffset>(NpgsqlDbType.TimestampTz).UtcDateTime,
                Hash = reader.Read<Guid>(NpgsqlDbType.Uuid),
                EntityId = reader.Read<Guid>(NpgsqlDbType.Uuid),
                SourceId = reader.Read<Guid>(NpgsqlDbType.Uuid),
                UpdateId = reader.Read<Guid>(NpgsqlDbType.Uuid),
            }, 250000);
        }

        private async Task ExportAllFeed(string filename)
        {
            await WriteToFile(filename, "feed(id, timestamp, data)", reader => new RawFeedEvent
            {
                Id = reader.Read<Guid>(NpgsqlDbType.Uuid),
                Timestamp = reader.Read<DateTimeOffset>(NpgsqlDbType.TimestampTz).UtcDateTime,
                Data = JsonToString(reader.Read<byte[]>(NpgsqlDbType.Bytea)),
            }, 50000);
        }

        private async Task ExportAllPusher(string filename)
        {
            await WriteToFile(filename, "pusher_events(id, channel, event, timestamp, raw, data)", reader => {
                var pu = new RawPusherEvent
                {
                    Id = reader.Read<Guid>(NpgsqlDbType.Uuid),
                    Channel = reader.Read<string>(NpgsqlDbType.Text),
                    Event = reader.Read<string>(NpgsqlDbType.Text),
                    Timestamp = reader.Read<DateTimeOffset>(NpgsqlDbType.TimestampTz).UtcDateTime,
                    Raw = reader.Read<string>(NpgsqlDbType.Text),
                };

                if (!reader.IsNull) 
                    pu.Data = JsonToString(reader.Read<byte[]>(NpgsqlDbType.Bytea));
                else
                    reader.Skip();

                return pu;
            }, 5000);
        }

        private async Task WriteToFile<T>(string filename, string table, Func<NpgsqlBinaryExporter, T> mapper, int bufSize)
        {
            try {
                _logger.Information("Exporting from {Table} to {Filename}...", table, filename);
                
                await using var conn = await _db.Obtain();
                _logger.Information("... (obtained db connection)");

                await using var reader = conn.BeginBinaryExport($"copy {table} to stdout (format binary)");
                _logger.Information("... (started binary export)");

                await using var file = File.Open(filename + ".zst", FileMode.Create, FileAccess.Write);
                _logger.Information("... (opened file)");

                await using var zstd = new CompressionStream(file);
                _logger.Information("... (opened compression stream)");

                // await using var gz = new GZipStream(file, CompressionLevel.Optimal);
                
                var rows = 0;

                _logger.Information("Starting read...");

                var buf = new MemoryStream();
                var bufCount = 0;
                while (await reader.StartRowAsync() > -1)
                {
                    // if (buf.Count % (bufSize / 20) == 0) {
                    //     _logger.Information("Buffering rows ({Rows} in buffer)", buf.Count);
                    // }

                    if (bufCount >= bufSize)
                    {
                        _logger.Information("Writing to {Filename} ({Rows} so far)", filename, rows);

                        if (bufCount > 0)
                        {
                            await zstd.WriteAsync(buf.ToArray());
                            buf.SetLength(0);
                            bufCount = 0;
                        }
                    }

                    var obj = mapper(reader);
                    buf.Write(System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(obj));
                    buf.WriteByte(0x0a);
                    bufCount++;
                    rows++;
                }
                
                if (bufCount > 0)
                {
                    await zstd.WriteAsync(buf.ToArray());
                    buf.SetLength(0);
                }
                
                _logger.Information("Done exporting {Table}.", table);
            } catch (Exception e) {
                _logger.Error(e, "Got exception");
            }
        }

        private string JsonToString(byte[] bytes) {
            // https://stackoverflow.com/a/35601852
            return Encoding.UTF8.GetString(bytes.AsSpan(1));
        }

        public class MsgpackJsonFormatter : IMessagePackFormatter<string>
        {
            private readonly char[] _tmp = new char[1];
            
            public void Serialize(ref MessagePackWriter writer, string value, MessagePackSerializerOptions options)
            {
                using var sr = new StringReader(value);
                
                // Skip first broken byte
                if (value[0] == '\x01')
                    sr.Read(_tmp, 0, 1);
                
                MessagePackSerializer.ConvertFromJson(sr, ref writer);
            }

            public string Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
            {
                using var sw = new StringWriter();
                MessagePackSerializer.ConvertToJson(ref reader, sw);
                return sw.ToString();
            }
        }
    }
}