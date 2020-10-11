using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using NodaTime;
using Npgsql;
using Serilog;
using SIBR.Storage.Data.Models;
using SIBR.Storage.Data.Query;
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

        public IAsyncEnumerable<EntityUpdateView> ExportAllUpdatesRaw(UpdateType type, EntityVersionQuery opts)
        {
            var q = new SqlKata.Query("updates")
                .ApplyBounds(opts, "timestamp")
                .ApplySorting(opts, "timestamp", "update_id")
                .Join("objects", "objects.hash", "updates.hash")
                .Where("type", type);

            if (opts.Ids != null)
                q.WhereIn("entity_id", opts.Ids);

            return _db.QueryKataAsync<EntityUpdateView>(q);
        }

        public IAsyncEnumerable<EntityUpdateView> ExportAllUpdatesGrouped(UpdateType type)
        {
            // todo: move to view
            return _db.QueryStreamAsync<EntityUpdateView>(@"
select
    type,
    timestamp,
    update_id,
    source_id,
    entity_id,
    hash,
    case
        when (lag(hash) over w) is distinct from hash then 
            (select data from objects o where o.hash = u.hash limit 1)
    end as data
from updates u
where type = @Type
window w as (partition by type, entity_id order by timestamp)
order by type, entity_id, timestamp
", new {Type = type});
        }

        public async Task<EntityUpdate> GetLatestUpdate(NpgsqlConnection conn, UpdateType type, Guid? entityId = null)
        {
            var query = entityId != null
                ? "select * from latest_view where type = @Type and entity_id = @EntityId limit 1"
                : "select * from latest_view where type = @Type limit 1";
            return await conn.QuerySingleOrDefaultAsync<EntityUpdate>(query, new {Type = type, EntityId = entityId});
        }

        public IAsyncEnumerable<EntityUpdateView> GetLatestUpdatesFor(UpdateType type) =>
            _db.QueryStreamAsync<EntityUpdateView>("select * from latest_view where type = @Type",
                new {Type = type});

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

        public IAsyncEnumerable<EntityVersionView> GetAllVersions(UpdateType type, string table, EntityVersionQuery query)
        {
            var q = new SqlKata.Query(table)
                .Where("type", type)
                .ApplyBounds(query, "first_seen")
                .ApplySorting(query, "first_seen", "update_id");

            if (query.Ids != null)
                q.WhereIn("entity_id", query.Ids);
            
            return _db.QueryKataAsync<EntityVersionView>(q);
        }

        public class EntityVersionQuery: IPaginatedQuery, IBoundedQuery<Instant>
        {
            public Guid[] Ids { get; set; }
            public Instant? Before { get; set; }
            public Instant? After { get; set; }
            public int? Count { get; set; }
            public SortOrder Order { get; set; }
            public PageToken Page { get; set; }
        }
    }
}