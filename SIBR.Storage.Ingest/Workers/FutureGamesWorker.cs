using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json.Linq;
using NodaTime;
using SIBR.Storage.Data;
using SIBR.Storage.Data.Models;

namespace SIBR.Storage.Ingest
{
    public class FutureGamesWorker : IntervalWorker
    {
        private readonly Guid _sourceId;
        private readonly Database _db;
        private readonly GameStore _gameStore;
        private readonly UpdateStore _updateStore;
        private readonly GameUpdateStore _gameUpdateStore;
        private readonly HttpClient _client;
        private readonly IClock _clock;

        public FutureGamesWorker(IServiceProvider services, IntervalWorkerConfiguration config, Guid sourceId) : base(services, config)
        {
            _sourceId = sourceId;
            _updateStore = services.GetRequiredService<UpdateStore>();
            _gameStore = services.GetRequiredService<GameStore>();
            _gameUpdateStore = services.GetRequiredService<GameUpdateStore>();
            _db = services.GetRequiredService<Database>();
            _client = services.GetRequiredService<HttpClient>();
            _clock = services.GetRequiredService<IClock>();
        }

        protected override async Task RunInterval()
        {
            await using var conn = await _db.Obtain();
            var simData = await _updateStore.GetLatestUpdate(conn, UpdateType.Sim);
            var (sim, season) = (simData.Data.Value<string?>("sim") ?? "thisidisstaticyo", simData.Data.Value<int>("season"));

            var jsonStr = await _client.GetStringAsync($"https://api.blaseball.com/api/games/schedule?sim={sim}&season={season}");
            var timestamp = _clock.GetCurrentInstant();
            
            var json = JObject.Parse(jsonStr);
            
            // Schedule returns an object of "day" arrays
            var updates = new List<GameUpdate>();
            foreach (var dayArray in json.Values())
            foreach (var gameObject in dayArray)
                updates.Add(GameUpdate.From(_sourceId, timestamp, gameObject));
            
            await using (var tx = await conn.BeginTransactionAsync())
            {
                await _gameUpdateStore.SaveGameUpdates(conn, updates);
                await tx.CommitAsync();
            }
            
            _logger.Information("Fetched season schedule for sim {Sim} season {Season}", sim, season);
        }
    }
}