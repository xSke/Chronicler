using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Dapper;
using NodaTime;
using SIBR.Storage.Data.Models;
using SIBR.Storage.Data.Utils;
using SqlKata;

namespace SIBR.Storage.Data
{
    public class IdolsTributesStore
    {
        private readonly Database _db;

        public IdolsTributesStore(Database db)
        {
            _db = db;
        }

        public IAsyncEnumerable<TributesUpdate> GetTributeUpdates(TributesQuery opts) =>
            GetTributeUpdatesInner("tributes_by_player", opts);

        public IAsyncEnumerable<TributesUpdate> GetTributeUpdatesHourly(TributesQuery opts) =>
            GetTributeUpdatesInner("tributes_hourly", opts);

        private IAsyncEnumerable<TributesUpdate> GetTributeUpdatesInner(string table, TributesQuery opts)
        {
            var q = new Query()
                .SelectRaw("timestamp, array_agg(player_id) as players, array_agg(peanuts) as peanuts")
                .From(table)
                .GroupBy("timestamp");

            if (opts.Reverse)
                q.OrderByDesc("timestamp");
            else
                q.OrderBy("timestamp");

            if (opts.Before != null) q.Where("timestamp", "<", opts.Before.Value);
            if (opts.After != null) q.Where("timestamp", ">", opts.After.Value);
            if (opts.Players != null) q.WhereIn("player_id", opts.Players);
            if (opts.Count != null) q.Limit(opts.Count.Value);

            return _db.QueryKataAsync<TributesUpdate>(q);
        }

        public class TributesQuery
        {
            public Guid[] Players;
            public Instant? Before;
            public Instant? After;
            public int? Count;
            public bool Reverse;
        }
    }
}