using System;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Serilog;
using SIBR.Storage.Data;
using SIBR.Storage.Data.Models;

namespace SIBR.Storage.Ingest
{
    public class GlobalEventsWorker: IntervalWorker
    {
        private readonly HttpClient _client;
        private readonly Database _db;
        private readonly MiscStore _miscStore;

        public GlobalEventsWorker(ILogger logger, HttpClient client, Database db, MiscStore miscStore) : base(logger)
        {
            _client = client;
            _db = db;
            _miscStore = miscStore;
            Interval = TimeSpan.FromMinutes(1);
        }

        protected override async Task RunInterval()
        {
            var json =  await _client.GetStringAsync("https://www.blaseball.com/database/globalEvents");
            var update = new MiscUpdate(MiscUpdate.GlobalEvents, DateTimeOffset.UtcNow, JToken.Parse(json));
            
            await using var conn = await _db.Obtain();
            await _miscStore.SaveMiscUpdates(conn, new[] {update});
        }
    }
}