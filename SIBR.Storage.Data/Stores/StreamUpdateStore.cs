using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using Newtonsoft.Json.Linq;
using Npgsql;
using Serilog;
using SIBR.Storage.Data.Models;
using SIBR.Storage.Data.Utils;

namespace SIBR.Storage.Data
{
    public class StreamUpdateStore : BaseStore
    {
        private readonly ILogger _logger;

        public StreamUpdateStore(ILogger logger)
        {
            _logger = logger;
        }

        public async Task SaveUpdates(NpgsqlConnection conn, IReadOnlyCollection<StreamUpdate> updates)
        {
            var objectRows = await SaveObjects(conn, updates);
            
            var rows = await conn.ExecuteAsync("insert into stream_updates(timestamp, hash) select unnest(@Timestamps), unnest(@Hashes) on conflict do nothing", new
            {
                Timestamps = updates.Select(o => o.Timestamp).ToArray(),
                Hashes = updates.Select(u => u.Hash).ToArray()
            });

            if (rows > 0) 
                _logger.Information("Imported {RowCount} stream data updates, {ObjectRowCount} new objects", rows, objectRows);
        }
    }
}