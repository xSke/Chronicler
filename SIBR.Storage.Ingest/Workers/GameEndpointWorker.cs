using System;
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

        private (int, int) _lastSeasonDay;
        private Instant? _lastSeasonDayQuery;

        public GameEndpointWorker(IServiceProvider services, Guid sourceId) : base(services)
        {
            _sourceId = sourceId;
            _db = services.GetRequiredService<Database>();
            _gameUpdateStore = services.GetRequiredService<GameUpdateStore>();
            _clock = services.GetRequiredService<IClock>();
            _client = services.GetRequiredService<HttpClient>();
            Interval = TimeSpan.FromSeconds(1);
        }

        protected override async Task RunInterval()
        {
            var (season, day) = await TryGetSeasonDay();
            await FetchGamesInner(season, day);
        }

        private async Task FetchGamesInner(int season, int day)
        {
            var sw = new Stopwatch();
            sw.Start();
            var gamesStr = await _client.GetStringAsync($"https://www.blaseball.com/database/games?season={season}&day={day}");
            sw.Stop();
            var timestamp = _clock.GetCurrentInstant();

            var gamesArr = JArray.Parse(gamesStr);
            _logger.Information("Polled games endpoint at season {Season} day {Day} (combined hash {Hash}, took {Duration})",
                season,
                day, SibrHash.HashAsGuid(gamesArr), sw.Elapsed);

            await using var conn = await _db.Obtain();
            await _gameUpdateStore.SaveGameUpdates(conn, gamesArr
                .Select(game => GameUpdate.From(_sourceId, timestamp, game))
                .ToList());
        }

        private async ValueTask<(int, int)> TryGetSeasonDay()
        {
            var now = _clock.GetCurrentInstant();

            if (_lastSeasonDayQuery == null ||
                now.InUtc().Minute != _lastSeasonDayQuery.Value.InUtc().Minute && now.InUtc().Second > 0)
            {
                _lastSeasonDay = await FetchSeasonDay();
                _lastSeasonDayQuery = now;
            }

            return _lastSeasonDay;
        }

        private async Task<(int, int)> FetchSeasonDay()
        {
            var simObjStr = await _client.GetStringAsync("https://www.blaseball.com/database/simulationData");
            var simObj = JObject.Parse(simObjStr);
            return (simObj.Value<int>("season"), simObj.Value<int>("day"));
        }
    }
}