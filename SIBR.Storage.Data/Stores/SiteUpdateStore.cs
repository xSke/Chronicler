using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using Npgsql;
using SIBR.Storage.Data.Models;

namespace SIBR.Storage.Data
{
    public class SiteUpdateStore : BaseStore
    {
        public SiteUpdateStore(IServiceProvider services) : base(services)
        {
        }
        
        public async Task SaveSiteUpdates(NpgsqlConnection conn, IReadOnlyCollection<SiteUpdate> updates)
        {
            await conn.ExecuteAsync(
                "insert into site_updates(timestamp, path, hash, data) select unnest(@Timestamps), unnest(@Paths), unnest(@Hashes), unnest(@Datas) on conflict do nothing",
                new
                {
                    Timestamps = updates.Select(u => u.Timestamp).ToArray(),
                    Paths = updates.Select(u => u.Path).ToArray(),
                    Hashes = updates.Select(u => u.Hash).ToArray(),
                    Datas = updates.Select(u => u.Data).ToArray()
                });
        }
    }
}