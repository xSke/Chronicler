using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Dapper;
using NodaTime;
using SIBR.Storage.Data.Models;

namespace SIBR.Storage.Data
{
    public class IdolsTributesStore
    {
        private readonly Database _db;

        public IdolsTributesStore(Database db)
        {
            _db = db;
        }

        public Task<IEnumerable<TributesUpdate>>
            GetTributeUpdates(Instant before, int count, Guid[] playerIds = null) =>
            GetTributeUpdatesInner("tributes_by_player", before, count, playerIds);

        public Task<IEnumerable<TributesUpdate>> GetTributeUpdatesHourly(Instant before, int count,
            Guid[] playerIds = null) =>
            GetTributeUpdatesInner("tributes_hourly", before, count, playerIds);

        private async Task<IEnumerable<TributesUpdate>> GetTributeUpdatesInner(string table, Instant before, int count,
            Guid[] playerIds = null)
        {
            var query =
                $"select timestamp, array_agg(player_id) as players, array_agg(peanuts) as peanuts from {table} where timestamp < @Before";
            if (playerIds != null)
                query += " and player_id = any(@PlayerIds)";
            query += " group by timestamp order by timestamp desc limit @Count";

            await using var conn = await _db.Obtain();
            return await conn.QueryAsync<TributesUpdate>(query,
                new
                {
                    Before = before,
                    PlayerIds = playerIds,
                    Count = count
                });
        }
    }
}