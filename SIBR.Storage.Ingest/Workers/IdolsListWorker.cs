using System;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Serilog;
using SIBR.Storage.Data;
using SIBR.Storage.Data.Models;

namespace SIBR.Storage.Ingest
{
    public class IdolsListWorker : IntervalWorker
    {
        private readonly HttpClient _client;
        private readonly Database _db;
        private readonly MiscStore _miscStore;

        public IdolsListWorker(ILogger logger, HttpClient client, MiscStore miscStore, Database db) : base(logger)
        {
            _client = client;
            _miscStore = miscStore;
            _db = db;
            Interval = TimeSpan.FromMinutes(1);
        }

        protected override async Task RunInterval()
        {
            var json =  await _client.GetStringAsync("https://www.blaseball.com/api/getIdols");
            var update = new MiscUpdate(MiscUpdate.Idols, DateTimeOffset.UtcNow, JToken.Parse(json));
            
            await using var conn = await _db.Obtain();
            await _miscStore.SaveMiscUpdates(conn, new[] {update});
        }
    }
}