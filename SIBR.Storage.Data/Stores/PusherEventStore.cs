using System.Threading.Tasks;
using Dapper;
using Newtonsoft.Json.Linq;
using NodaTime;

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
    }
}