using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json.Linq;
using NodaTime;
using SIBR.Storage.Data;
using SIBR.Storage.Data.Models;
using SIBR.Storage.Data.Utils;

namespace SIBR.Storage.Ingest
{
    public class GameEndpointWorker : IntervalWorker
    {
        private readonly Guid _sourceId;
        private readonly Database _db;
        private readonly GameUpdateStore _gameUpdateStore;
        private readonly HttpClient _client;
        private readonly IClock _clock;
        private readonly UpdateStore _updateStore;
        private readonly GameStore _gameStore;

        public GameEndpointWorker(IServiceProvider services, IntervalWorkerConfiguration config, Guid sourceId) : base(services, config)
        {
            _sourceId = sourceId;
            _db = services.GetRequiredService<Database>();
            _gameUpdateStore = services.GetRequiredService<GameUpdateStore>();
            _clock = services.GetRequiredService<IClock>();
            _client = services.GetRequiredService<HttpClient>();
            _updateStore = services.GetRequiredService<UpdateStore>();
            _gameStore = services.GetRequiredService<GameStore>();
        }

        protected override async Task RunInterval()
        {
            EntityUpdate sim;
            await using (var conn = await _db.Obtain())
               sim = await _updateStore.GetLatestUpdate(conn, UpdateType.Sim);
            
            await FetchGamesInner(sim.Data.Value<int>("season"), sim.Data.Value<int>("day"));
        }

        private async Task FetchGamesInner(int season, int day)
        {
            var gameUpdates = await FetchGamesAt(season, day);

            await using var conn = await _db.Obtain();
            await using (var tx = await conn.BeginTransactionAsync())
            {
                await _gameUpdateStore.SaveGameUpdates(conn, gameUpdates);
                await tx.CommitAsync();
            }

            await _gameStore.TryAddNewGameIds(conn, gameUpdates.Select(gu => gu.GameId));
        }

        private async Task<List<GameUpdate>> FetchGamesAt(int season, int day)
        {
            var sw = new Stopwatch();
            sw.Start();
            var jsonStr  = await _client.GetStringAsync($"https://www.blaseball.com/database/games?season={season}&day={day}");
            sw.Stop();
            var timestamp = _clock.GetCurrentInstant();

            var json = JArray.Parse(jsonStr);
            _logger.Information("Polled games endpoint at season {Season} day {Day} (combined hash {Hash}, took {Duration})",
                season,
                day, SibrHash.HashAsGuid(json), sw.Elapsed);
            
            var updates = json
                .Select(game => GameUpdate.From(_sourceId, timestamp, game))
                .ToList();
            return updates;
        }
    }
}