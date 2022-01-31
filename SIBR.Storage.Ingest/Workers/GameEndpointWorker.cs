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
            EntityUpdate simData;
            await using (var conn = await _db.Obtain())
               simData = await _updateStore.GetLatestUpdate(conn, UpdateType.Sim);

            var sim = simData.Data.Value<string>("id") ?? "thisidisstaticyo";
            var season = simData.Data.Value<int>("season");
            // var tournament = simData.Data.Value<int>("tournament");
            var tournament = -1;
            var day = simData.Data.Value<int>("day");
            
            await FetchGamesInner(sim, tournament, season, day);
        }

        private async Task FetchGamesInner(string sim, int tournament, int season, int day)
        {
            var gameUpdates = await FetchGamesAt(sim, tournament, season, day);

            await using var conn = await _db.Obtain();
            await using var tx = await conn.BeginTransactionAsync();
            
            await _gameUpdateStore.SaveGameUpdates(conn, gameUpdates);
            await tx.CommitAsync();
        }

        private async Task<List<GameUpdate>> FetchGamesAt(string sim, int tournament, int season, int day)
        {
            var sw = new Stopwatch();
            sw.Start();

            var cacheBust = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

            var url = $"https://api.blaseball.com/database/games?season={season}&day={day}&tournament={tournament}&cache={cacheBust}";
            var jsonStr  = await _client.GetStringAsync(url);
            
            sw.Stop();
            var timestamp = _clock.GetCurrentInstant();

            var json = JArray.Parse(jsonStr);
            var games = json.Where(g => g["sim"].Value<string?>() == sim).ToList();
            
            var maxPlayCount = games.Count > 0 ? games.Max(t => t["playCount"].Value<int?>() ?? -1) : -1;
            _logger.Information("Polled games endpoint at sim {Sim} season {Season} tournament {Tournament} day {Day} (combined hash {Hash}, max PC {MaxPlayCount}, took {Duration})",
                sim, season, tournament,
                day, SibrHash.HashAsGuid(json), maxPlayCount, sw.Elapsed);
            
            var updates = games
                .Select(game => GameUpdate.From(_sourceId, timestamp, game))
                .ToList();
            return updates;
        }
    }
}