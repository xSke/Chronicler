using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using Newtonsoft.Json.Linq;
using Npgsql;
using NpgsqlTypes;
using Serilog;
using SIBR.Storage.Data.Utils;

namespace SIBR.Storage.Data
{
    public class StreamUpdateStore : BaseStore
    {
        private readonly ILogger _logger;
        private readonly Database _db;

        public StreamUpdateStore(ILogger logger, Database db)
        {
            _logger = logger;
            _db = db;
        }

        public async Task SaveUpdatesBulk(IAsyncEnumerable<(DateTimeOffset Timestamp, JObject Update)> updates)
        {
            await using var conn = await _db.Obtain();
            await using var tx = await conn.BeginTransactionAsync();

            await conn.ExecuteAsync("create temporary table tmp_updates (hash uuid, timestamp timestamptz, data jsonb)");

            await using (var writer = conn.BeginBinaryImport("copy tmp_updates (hash, timestamp, data) from stdin (format binary)"))
            {
                await foreach (var (timestamp, update) in updates)
                {
                    await writer.StartRowAsync();
                    await writer.WriteAsync(SibrHash.HashAsGuid(update), NpgsqlDbType.Uuid);
                    await writer.WriteAsync(timestamp, NpgsqlDbType.TimestampTz);
                    await writer.WriteAsync(update, NpgsqlDbType.Jsonb);
                }

                await writer.CompleteAsync();
            }

            var rows = await conn.ExecuteAsync("insert into stream_updates(timestamp, last_timestamp, hash, data) select min(tmp_updates.timestamp), max(tmp_updates.timestamp), tmp_updates.hash, (array_agg(tmp_updates.data))[1] as data from tmp_updates where not exists (select 1 from stream_updates where stream_updates.hash = tmp_updates.hash) group by tmp_updates.hash ");
            if (rows > 0) 
                _logger.Information("Imported {RowCount} stream data updates", rows);

            await conn.ExecuteAsync("drop table tmp_updates");
            await tx.CommitAsync();
        }

        public async Task SaveUpdate(NpgsqlConnection conn, DateTimeOffset timestamp, JObject update)
        {
            var hash = SibrHash.HashAsGuid(update);

            var newRow = await conn.ExecuteAsync(
                @"
insert into stream_updates (timestamp, last_timestamp, hash, data)
select @timestamp, @timestamp, @hash, null
where not exists (select 1 from stream_updates where hash = @hash and timestamp = @timestamp)
",
//                 @"
// insert into stream_updates (timestamp, last_timestamp, hash, data) 
// values (@timestamp, @timestamp, @hash, null) 
// on conflict (hash) do update 
//     set timestamp = least(stream_updates.timestamp, @timestamp), 
//         last_timestamp = greatest(stream_updates.last_timestamp, excluded.last_timestamp) where stream_updates.last_timestamp != excluded.last_timestamp or stream_updates.timestamp != excluded.timestamp
// returning (xmax = 0)",
                new {timestamp, hash});

            if (newRow > 0)
            {
                await conn.ExecuteAsync("update stream_updates set data = @data where hash = @hash", new {hash, data = update});
                _logger.Information("Saved stream data at {Timestamp} with hash {Hash}", timestamp, hash);
            }
        }
    }
}