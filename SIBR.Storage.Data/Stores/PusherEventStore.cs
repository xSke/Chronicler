using System.Collections.Generic;
using System.Threading.Tasks;
using Dapper;
using Newtonsoft.Json.Linq;
using NodaTime;
using Npgsql;
using SIBR.Storage.Data.Models.Read;
using SIBR.Storage.Data.Query;
using SIBR.Storage.Data.Utils;

namespace SIBR.Storage.Data
{
    public class PusherEventStore
    {
        private readonly Database _db;

        public PusherEventStore(Database db)
        {
            _db = db;
        }

        public async Task SaveEvent(string channel, string @event, Instant timestamp, JToken? data, string raw)
        {
            await using var conn = await _db.Obtain();
            await conn.ExecuteAsync(
                "insert into pusher_events (channel, event, timestamp, raw, data) values (@Channel, @Event, @Timestamp, @Raw, @Data)", new
                {
                    Channel = channel,
                    Event = @event,
                    Timestamp = timestamp,
                    Raw = raw,
                    Data = data
                });
        }

        public IAsyncEnumerable<PusherEvent> GetEvents(NpgsqlConnection conn, EventQuery query)
        {
            var q = new SqlKata.Query("pusher_events")
                .Select("*");

            if (query.Order == SortOrder.Asc)
                q.OrderBy("timestamp");
            else
                q.OrderByDesc("timestamp");

            if (query.Count != null)
                q.Limit(query.Count!.Value);

            if (query.Channel != null)
                q.WhereIn("channel", query.Channel);
            if (query.Event != null)
                q.WhereIn("event", query.Event);

            q.ApplyBounds(query, "timestamp");
            
            return conn.QueryKataAsync<PusherEvent>(q);
        }

        public class EventQuery: IBoundedQuery<Instant>, ISortedQuery
        {
            public string[]? Channel { get; set; }
            
            public string[]? Event { get; set; }
            public Instant? Before { get; set; }
            public Instant? After { get; set; }
            public int? Count { get; set; }
            public SortOrder Order { get; set;  }
        }
    }
}