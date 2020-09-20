using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using Newtonsoft.Json.Linq;
using Npgsql;
using Serilog;
using SIBR.Storage.Data.Models;

namespace SIBR.Storage.Data
{
    public class MiscStore : BaseStore
    {
        public async Task<UpdateStoreResult> SaveMiscUpdates(NpgsqlConnection conn, IReadOnlyCollection<MiscUpdate> updates)
        {
            var objectRows = await SaveObjects(conn, updates);
            var rows = await conn.ExecuteAsync(
                "insert into misc_updates (timestamp, type, hash) select unnest(@Timestamps), unnest(@Types), unnest(@Hashes) on conflict do nothing",
                new
                {
                    Timestamps = updates.Select(u => u.Timestamp).ToArray(),
                    Types = updates.Select(u => u.Type).ToArray(),
                    Hashes = updates.Select(u => u.Hash).ToArray()
                });
            
            return new UpdateStoreResult(rows, objectRows); 
        }
    }
}