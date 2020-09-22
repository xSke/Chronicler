using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using Npgsql;
using SIBR.Storage.Data.Models;

namespace SIBR.Storage.Data
{
    public class UpdateStore : BaseStore
    {
        public UpdateStore(IServiceProvider services) : base(services)
        {
        }

        public async Task<EntityUpdate> GetLastUpdate(NpgsqlConnection conn, UpdateType type)
        {
            return await conn.QuerySingleOrDefaultAsync<EntityUpdate>(
                "select * from updates where type = @Type order by timestamp desc limit 1", new
                {
                    Type = type
                });
        }

        public Task SaveUpdate(NpgsqlConnection conn, EntityUpdate update) =>
            SaveUpdates(conn, new[] {update});

        public async Task<int> SaveUpdates(NpgsqlConnection conn, IReadOnlyCollection<EntityUpdate> updates, bool log = true)
        {
            if (log)
                LogUpdates(updates);
            
            await SaveObjects(conn, updates);

            var rows = await conn.ExecuteAsync(
                "insert into updates (source_id, type, timestamp, hash, entity_id) select unnest(@SourceId), unnest(@Type), unnest(@Timestamp), unnest(@Hash), unnest(@EntityId) on conflict do nothing",
                new
                {
                    SourceId = updates.Select(u => u.SourceId).ToArray(),
                    
                    // byte[] gets mapped to bytea and not smallint[], so send a short[]
                    Type = updates.Select(u => (short) u.Type).ToArray(),

                    Timestamp = updates.Select(u => u.Timestamp).ToArray(),
                    Hash = updates.Select(u => u.Hash).ToArray(),

                    // Use all-zero UUID as a "null value" instead of actual null, blame Npgsql
                    EntityId = updates.Select(u => u.EntityId ?? default).ToArray()
                });

            return rows;
        }

        private void LogUpdates(IReadOnlyCollection<EntityUpdate> updates)
        {
            foreach (var update in updates)
            {
                _logger.Debug("Saving update: {@Update}", new
                {
                    update.Type,
                    update.SourceId,
                    update.Timestamp,
                    update.EntityId,
                    update.Hash
                });
            }
        }
    }
}