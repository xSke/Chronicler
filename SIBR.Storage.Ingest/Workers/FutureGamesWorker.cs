using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Reflection.Metadata;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json.Linq;
using NodaTime;
using SIBR.Storage.Data;
using SIBR.Storage.Data.Models;
using SIBR.Storage.Data.Utils;

namespace SIBR.Storage.Ingest
{
    public class FutureGamesWorker : IntervalWorker
    {
        private readonly Guid _sourceId;
        private readonly Database _db;
        private readonly UpdateStore _updateStore;
        private readonly GameUpdateStore _gameUpdateStore;
        private readonly HttpClient _client;
        private readonly IClock _clock;

        public FutureGamesWorker(IServiceProvider services, Guid sourceId) : base(services)
        {
            _sourceId = sourceId;
            _updateStore = services.GetRequiredService<UpdateStore>();
            _gameUpdateStore = services.GetRequiredService<GameUpdateStore>();
            _db = services.GetRequiredService<Database>();
            _client = services.GetRequiredService<HttpClient>();
            _clock = services.GetRequiredService<IClock>();

            Interval = TimeSpan.FromHours(1);
            Offset = TimeSpan.FromSeconds(5);
        }

        protected override async Task RunInterval()
        {
            await using var conn = await _db.Obtain();
            var sim = await _updateStore.GetLastUpdate(conn, UpdateType.Sim);
            var (season, dayStart) = (sim.Data.Value<int>("season"), sim.Data.Value<int>("day"));

            var updates = new List<GameUpdate>();
            for (var day = dayStart + 1; day < 100; day++)
            {
                var jsonStr = await _client.GetStringAsync($"https://www.blaseball.com/database/games?season={season}&day={day}");
                var timestamp = _clock.GetCurrentInstant();
                var json = JArray.Parse(jsonStr);
                _logger.Information("Polled future games at season {Season} day {Day}", season, day);

                updates.AddRange(json.Select(game => GameUpdate.From(_sourceId, timestamp, game)));
            }


            await using (var tx = await conn.BeginTransactionAsync())
            {
                await _gameUpdateStore.SaveGameUpdates(conn, updates);
                await tx.CommitAsync();
            }

            await _gameUpdateStore.RefreshViewsIfNewGames(conn, updates);
        }
    }
}