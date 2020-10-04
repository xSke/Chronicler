using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using NodaTime;
using Npgsql;
using SIBR.Storage.Data.Models;
using SIBR.Storage.Data.Query;
using SIBR.Storage.Data.Utils;

namespace SIBR.Storage.Data
{
    public class SiteUpdateStore
    {
        private readonly Database _db;
        private readonly ObjectStore _objectStore;
        
        public SiteUpdateStore(Database db, ObjectStore objectStore)
        {
            _db = db;
            _objectStore = objectStore;
        }

        public IAsyncEnumerable<SiteUpdateView> GetUniqueSiteUpdates(SiteUpdateQueryOpts opts)
        {
            var q = new SqlKata.Query("site_updates_unique")
                .Select("hash", "path", "timestamp", "size")
                .ApplySorting(opts, "timestamp", "hash")
                .ApplyBounds(opts, "timestamp");

            return _db.QueryKataAsync<SiteUpdateView>(q);
        }

        public async Task<byte[]> GetObjectData(Guid hash)
        {
            await using var conn = await _db.Obtain();
            return await conn.QuerySingleOrDefaultAsync<byte[]>("select data from binary_objects where hash = @Hash",
                new {Hash = hash});
        }
        
        public async Task SaveSiteUpdates(NpgsqlConnection conn, IReadOnlyCollection<SiteUpdate> updates)
        {
            await _objectStore.SaveBinaryObjects(conn, updates);
            await conn.ExecuteAsync(
                "insert into site_updates(source_id, timestamp, path, hash, last_modified) select unnest(@SourceId), unnest(@Timestamp), unnest(@Path), unnest(@Hash), unnest(@LastModified) on conflict do nothing",
                new
                {
                    SourceId = updates.Select(u => u.SourceId).ToArray(),
                    Timestamp = updates.Select(u => u.Timestamp).ToArray(),
                    Path = updates.Select(u => u.Path).ToArray(),
                    Hash = updates.Select(u => u.Hash).ToArray(),
                    LastModified = updates.Select(u => u.LastModified ?? default).ToArray()
                });
        }
        
        public class SiteUpdateQueryOpts: ISortedQuery, IBoundedQuery<Instant>
        {
            public int? Count { get; set; }
            public SortOrder Order { get; set; }
            public Instant? Before { get; set; }
            public Instant? After { get; set; }
        }
    }
}