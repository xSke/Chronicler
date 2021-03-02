using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using Newtonsoft.Json.Linq;
using Npgsql;
using SIBR.Storage.Data.Models;

namespace SIBR.Storage.Data
{
    public class FeedStore
    {
        public async Task<FeedItem> GetLatestFeedItem(NpgsqlConnection conn)
        {
            return await conn.QueryFirstOrDefaultAsync<FeedItem>("select * from feed order by timestamp desc limit 1");
        }

        public async Task<int> SaveFeedItems(NpgsqlConnection conn, IReadOnlyCollection<FeedItem> items)
        {
            return await conn.ExecuteAsync(
                "insert into feed(id, timestamp, data) select unnest(@Ids), unnest(@Timestamps), jsonb_array_elements(@Datas) on conflict do nothing", new
                {
                    Ids = items.Select(i => i.Id).ToArray(),
                    Timestamps = items.Select(i => i.Timestamp).ToArray(),
                    Datas = new JArray(items.Select(i => i.Data))
                });
        }
    }
}