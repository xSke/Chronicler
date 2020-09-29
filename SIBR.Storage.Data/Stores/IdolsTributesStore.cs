using System;
using System.Collections.Generic;
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
                .SelectRaw("update_id, timestamp, array_agg(player_id) as players, array_agg(peanuts) as peanuts")
                .From(table)
                .GroupBy("timestamp", "update_id")
                .ApplyFrom(opts, "timestamp", "updates");
            
            if (opts.Players != null)
                q.WhereIn("player_id", opts.Players);

            return _db.QueryKataAsync<TributesUpdate>(q);
        }

        public class TributesQuery: IUpdateQueryOpts
        {
            public Guid[] Players { get; set; }
            public Instant? Before { get; set; }
            public Instant? After { get; set; }
            public int? Count { get; set; }
            public bool Reverse { get; set; }
            public Guid? PageUpdateId { get; set; }
        }
    }
}