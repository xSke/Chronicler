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
        public async Task<UpdateStoreResult> SaveUpdates(NpgsqlConnection conn, IReadOnlyCollection<StreamUpdate> updates)
        {
            var objectRows = await SaveObjects(conn, updates);
            
            var rows = await conn.ExecuteAsync("insert into stream_updates(timestamp, hash) select unnest(@Timestamps), unnest(@Hashes) on conflict do nothing", new
            {
                Timestamps = updates.Select(o => o.Timestamp).ToArray(),
                Hashes = updates.Select(u => u.Hash).ToArray()
            });

            return new UpdateStoreResult(rows, objectRows);
        }
    }
}