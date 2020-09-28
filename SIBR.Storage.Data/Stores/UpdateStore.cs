using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using Npgsql;
using Serilog;
using SIBR.Storage.Data.Models;
using SIBR.Storage.Data.Utils;

namespace SIBR.Storage.Data
{
    public class UpdateStore
    {
        private readonly ILogger _logger;
        private readonly ObjectStore _objectStore;
        private readonly Database _db;

        public UpdateStore(ILogger logger, ObjectStore objectStore, Database db)
        {
            _logger = logger.ForContext<UpdateStore>();
            _objectStore = objectStore;
            _db = db;
        }

        public IAsyncEnumerable<EntityUpdateView> ExportAllUpdatesRaw(UpdateType type)
        {
            return _db.QueryStreamAsync<EntityUpdateView>("select * from updates inner join objects using (hash) where type = @Type order by timestamp", new
            {
                Type = type
            });
        }

        public IAsyncEnumerable<EntityVersion> ExportAllUpdatesGrouped(UpdateType type)
        {
            // todo: move to view
            return _db.QueryStreamAsync<EntityVersion>(@"
select
    type,
    entity_id,
    version,
    hash,
    (select data from objects o where o.hash = observations.hash),
    array_agg(observations.timestamp) as observations,
    array_agg(observations.source_id) as observation_sources
from (
    select 
        *,
        (sum(version_increment) over (partition by type, entity_id order by timestamp)) - 1 
            as version
    from (
        select
            *,
            case 
                when (lag(hash) over (partition by type, entity_id order by timestamp)) is distinct from hash then 1
            end as version_increment
        from updates
    ) as with_version_increment
) observations
where type = @Type
group by type, entity_id, version, hash
order by type, entity_id, version;
", new {Type = type});
        }

        public async Task<EntityUpdate> GetLastUpdate(NpgsqlConnection conn, UpdateType type, Guid? entityId = null)
        {
            var query = entityId != null
                ? "select * from updates inner join objects using (hash) where type = @Type and entity_id = @EntityId order by timestamp desc limit 1"
                : "select * from updates inner join objects using (hash) where type = @Type order by timestamp desc limit 1";
            return await conn.QuerySingleOrDefaultAsync<EntityUpdate>(query, new {Type = type, EntityId = entityId});
        }

        public Task SaveUpdate(NpgsqlConnection conn, EntityUpdate update) =>
            SaveUpdates(conn, new[] {update});

        public async Task<int> SaveUpdates(NpgsqlConnection conn, IReadOnlyCollection<EntityUpdate> updates,
            bool log = true)
        {
            if (log)
                LogUpdates(updates);

            await _objectStore.SaveObjects(conn, updates);

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