using System;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json.Linq;
using NodaTime;
using SIBR.Storage.Data;
using SIBR.Storage.Data.Models;
using SIBR.Storage.Ingest.Utils;

namespace SIBR.Storage.Ingest
{
    public class AvailableBetsWorker: IntervalWorker
    {
        private readonly Guid _sourceId;
        private readonly Database _db;
        private readonly UpdateStore _updateStore;
        private readonly HttpClient _client;
        private readonly IClock _clock;

        public AvailableBetsWorker(IServiceProvider services, IntervalWorkerConfiguration config, Guid sourceId) : base(services, config)
        {
            _sourceId = sourceId;
            _updateStore = services.GetRequiredService<UpdateStore>();
            _db = services.GetRequiredService<Database>();
            _client = services.GetRequiredService<HttpClient>();
            _clock = services.GetRequiredService<IClock>();

        }

        protected override async Task RunInterval()
        {
            var (timestamp, data) = await _client.GetJsonAsync("https://api.blaseball.com/championbets/availableBets");
            var sortedData = new JArray(data.OrderBy(x => x["teamId"]));

            var update = EntityUpdate.From(UpdateType.AvailableChampionBets, _sourceId, timestamp, sortedData);
            
            await using var conn = await _db.Obtain();
            await _updateStore.SaveUpdate(conn, update);
            
            _logger.Information("Saved available champion bets");
        }
    }
}