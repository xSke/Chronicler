using System;
using System.Collections.Generic;
using NodaTime;
using SIBR.Storage.Data.Models;
using SIBR.Storage.Data.Query;
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

        public IAsyncEnumerable<TributesUpdateView> GetTributeUpdates(TributesQuery opts) =>
            GetTributeUpdatesInner("tributes_by_player", opts);

        public IAsyncEnumerable<TributesUpdateView> GetTributeUpdatesHourly(TributesQuery opts) =>
            GetTributeUpdatesInner("tributes_hourly", opts);

        private IAsyncEnumerable<TributesUpdateView> GetTributeUpdatesInner(string table, TributesQuery opts)
        {
            var q = new SqlKata.Query(table)
                .SelectRaw("update_id, timestamp, array_agg(player_id) as players, array_agg(peanuts) as peanuts")
                .GroupBy("timestamp", "update_id")
                .ApplySorting(opts, "timestamp", "update_id")
                .ApplyBounds(opts, "timestamp");
            
            if (opts.Players != null)
                q.WhereIn("player_id", opts.Players);

            return _db.QueryKataAsync<TributesUpdateView>(q);
        }

        public class TributesQuery: IBoundedQuery<Instant>, IPaginatedQuery
        {
            public Guid[] Players { get; set; }
            public Instant? Before { get; set; }
            public Instant? After { get; set; }
            public int? Count { get; set; }
            public SortOrder Order { get; set; }
            public PageToken Page { get; set; }
        }
    }
}