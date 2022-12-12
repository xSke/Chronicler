using System;
using System.Collections.Generic;
using System.Diagnostics;
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
    public class VersionStore
    {
        private readonly ILogger _logger;

        public VersionStore(ILogger logger)
        {
            _logger = logger.ForContext<VersionStore>();
        }

        public IAsyncEnumerable<EntityVersion> GetEntityVersions(NpgsqlConnection conn, UpdateType type, VersionQuery ps)
        {
            var q = new SqlKata.Query("versions")
                .Select("*")
                .Join("objects", "versions.hash", "objects.hash")
                .Where("type", type)
                .ApplySorting(ps, "valid_from", "entity_id")
                .ApplyBounds(ps, "valid_from");

            if (ps.Id != null)
                q.WhereIn("entity_id", ps.Id);
            
            return conn.QueryKataAsync<EntityVersion>(q);
        }

        public IAsyncEnumerable<EntityVersion> GetEntities(NpgsqlConnection conn, UpdateType type, EntityQuery ps)
        {
            var q = new SqlKata.Query("versions")
                .Select("*")
                .Join("objects", "versions.hash", "objects.hash")
                .Where("type", type);
            
            // Specifically don't do a time-based sort here, only by entity ID
            // so shifts in the current version don't break things
            if (ps.Page != null) 
                q.Where("entity_id", ">", ps.Page.EntityId);

            if (ps.At != null)
                q.Where("valid_from", "<=", ps.At).WhereRaw("coalesce(valid_to, 'infinity') > ?", ps.At);
            else
                q.WhereNull("valid_to");

            if (ps.Id != null)
                q.WhereIn("entity_id", ps.Id);

            if (ps.Count != null)
                q.Limit(ps.Count.Value);

            return conn.QueryKataAsync<EntityVersion>(q);
        }

        public IAsyncEnumerable<EntityVersion> GetUpdates(NpgsqlConnection conn, UpdateQuery ps)
        {
            var q = new SqlKata.Query("updates")
                .Select("*")
                .Join("objects", "updates.hash", "objects.hash")
                .Where("type", ps.Types)
                .ApplySorting(ps, "timestamp", "update_id")
                .ApplyBounds(ps, "timestamp");

            if (ps.At != null)
                q.Where("valid_from", "<=", ps.At).WhereRaw("coalesce(valid_to, 'infinity') > ?", ps.At);
            else
                q.WhereNull("valid_to");

            if (ps.Id != null)
                q.WhereIn("entity_id", ps.Id);

            if (ps.Count != null)
                q.Limit(ps.Count.Value);

            return conn.QueryKataAsync<EntityVersion>(q);
        }
        
        public async Task RebuildAll(NpgsqlConnection conn, UpdateType type)
        {
            var ids = (await conn.QueryAsync<Guid>("select entity_id from entities where type = @Type", new
            {
                Type = type
            })).ToList();
            
            _logger.Information("Rebuilding all entities of type {Type} (found {EntityCount})", type, ids.Count);

            var sw = new Stopwatch();
            sw.Start();
            
            foreach (var id in ids)
                await RebuildEntity(conn, type, id);

            sw.Stop();
            
            _logger.Information("Rebuilt all entities of type {Type} ({Count} entities, took {Duration})", 
                type, ids.Count, sw.Elapsed);
        }
        
        public async Task RebuildEntity(NpgsqlConnection conn, UpdateType type, Guid id)
        {
            var sw = new Stopwatch();
            
            sw.Start();
            var count = await conn.QuerySingleAsync<int>("select rebuild_entity(@Type::smallint, @Id)", new
            {
                Type = type,
                Id = id
            });
            sw.Stop();

            var ignore = type switch
            {
                UpdateType.GameStatsheet => true,
                UpdateType.PlayerStatsheet => true,
                UpdateType.TeamStatsheet => true,
                UpdateType.SeasonStatsheet => true,
                UpdateType.Game => true,
                _ => false
            };
            if (!ignore)
                _logger.Information("Rebuilt versions for {Type} {Id} ({VersionCount} versions, took {Duration})", type, id, count, sw.Elapsed);
        }

        public class EntityQuery
        {
            public Guid[]? Id { get; set; }
            public Instant? At { get; set; }
            public int? Count { get; set; }
            public PageToken Page { get; set; }
        }
        
        public class VersionQuery: IPaginatedQuery, IBoundedQuery<Instant>
        {
            public Guid[]? Id { get; set; }
            public Instant? Before { get; set; }
            public Instant? After { get; set; }
            public int? Count { get; set; }
            public SortOrder Order { get; set;  }
            public PageToken Page { get; set; }
        }
        
        public class UpdateQuery: IPaginatedQuery, IBoundedQuery<Instant>
        {
            public Guid[]? Id { get; set; }
            public UpdateType[]? Id { get; set; }
            public Instant? Before { get; set; }
            public Instant? After { get; set; }
            public int? Count { get; set; }
            public SortOrder Order { get; set;  }
            public PageToken Page { get; set; }
        }
    }
}