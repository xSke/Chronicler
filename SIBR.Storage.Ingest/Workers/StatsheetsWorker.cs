using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json.Linq;
using NodaTime;
using Npgsql;
using SIBR.Storage.Data;
using SIBR.Storage.Data.Models;

namespace SIBR.Storage.Ingest
{
    public class StatsheetsWorker: IntervalWorker
    {
        private readonly Guid _sourceId;
        private readonly Database _db; 
        private readonly UpdateStore _updateStore;
        private readonly HttpClient _client;
        private readonly IClock _clock;
        
        public StatsheetsWorker(IServiceProvider services, IntervalWorkerConfiguration config, Guid sourceId) : base(services, config)
        {
            _sourceId = sourceId;
            _db = services.GetRequiredService<Database>();
            _client = services.GetRequiredService<HttpClient>();
            _updateStore = services.GetRequiredService<UpdateStore>();
            _clock = services.GetRequiredService<IClock>();
        }

        protected override async Task RunInterval()
        {
            await using var conn = await _db.Obtain();
            var sim = await _updateStore.GetLatestUpdate(conn, UpdateType.Sim);
            var seasonObj = await _updateStore.GetLatestUpdate(conn, UpdateType.Season);

            var season = sim.Data.Value<int>("season");
            var day = sim.Data.Value<int>("day");

            var gameStatsheetIds = await FetchGameStatsheetIds(season, day);
            var gameStatsheets = await FetchGameStatsheets(gameStatsheetIds);
            var gameStatsheetsTimestamp = _clock.GetCurrentInstant();
            await SaveGameStatsheets(conn, gameStatsheetsTimestamp, gameStatsheets);

            var seasonStatsheet = await FetchSeasonStatsheet(seasonObj.Data["stats"]!.Value<string>());
            var seasonStatsheetTimestamp = _clock.GetCurrentInstant();
            if (seasonStatsheet != null)
                await SaveSeasonStatsheet(conn, seasonStatsheetTimestamp, seasonStatsheet);

            var teamStatsheetIds = gameStatsheets.SelectMany(sheet => new[]
            {
                sheet["homeTeamStats"]!.Value<string>(),
                sheet["awayTeamStats"]!.Value<string>()
            }).ToList();

            if (seasonStatsheet != null)
                teamStatsheetIds.AddRange(seasonStatsheet["teamStats"]!.Values<string>());
            
            var teamStatsheets = await FetchTeamStatsheets(teamStatsheetIds);
            var teamStatsheetsTimestamp = _clock.GetCurrentInstant();
            await SaveTeamStatsheets(conn, teamStatsheetsTimestamp, teamStatsheets);

            var playerStatsheetIds = teamStatsheets.SelectMany(sheet => sheet["playerStats"]!.Values<string>()).ToArray();
            var playerStatsheetsTimestamp = _clock.GetCurrentInstant();
            var playerStatsheets = await FetchPlayerStatsheets(playerStatsheetIds);
            await SavePlayerStatsheets(conn, playerStatsheetsTimestamp, playerStatsheets);
        }

        private async Task<JToken> FetchSeasonStatsheet(string id)
        {
            var statsheetsJson = await _client.GetStringAsync($"https://www.blaseball.com/database/seasonStatsheets?ids={id}");
            var statsheetsObjs = JArray.Parse(statsheetsJson);
            return statsheetsObjs.FirstOrDefault();
        }

        private async Task<string[]> FetchGameStatsheetIds(int season, int day)
        {
            var gamesJson = await _client.GetStringAsync(
                $"https://www.blaseball.com/database/games?season={season}&day={day}");
            var gamesObjs = JArray.Parse(gamesJson);

            return gamesObjs.Select(game => game["statsheet"]!.Value<string>()).ToArray();
        }
        
        private async Task<JToken[]> FetchGameStatsheets(IEnumerable<string> ids)
        {
            var statsheetsJson = await _client.GetStringAsync($"https://www.blaseball.com/database/gameStatsheets?ids={string.Join(',', ids)}");
            var statsheetsObjs = JArray.Parse(statsheetsJson);
            return statsheetsObjs.ToArray();
        }
        
        private async Task<JToken[]> FetchTeamStatsheets(IEnumerable<string> ids)
        {
            var statsheetsJson = await _client.GetStringAsync($"https://www.blaseball.com/database/teamStatsheets?ids={string.Join(',', ids)}");
            var statsheetsObjs = JArray.Parse(statsheetsJson);
            return statsheetsObjs.ToArray();
        }
        
        private async Task<JToken[]> FetchPlayerStatsheets(IEnumerable<string> ids)
        {
            var sheets = new List<JToken>();
            await foreach (var chunk in ids.ToAsyncEnumerable().Buffer(100))
            {
                var statsheetsJson =
                    await _client.GetStringAsync(
                        $"https://www.blaseball.com/database/playerStatsheets?ids={string.Join(',', chunk)}");
                var statsheetsObjs = JArray.Parse(statsheetsJson);
                sheets.AddRange(statsheetsObjs);
            }
            return sheets.ToArray();
        }

        private async Task SaveGameStatsheets(NpgsqlConnection conn, Instant timestamp, IEnumerable<JToken>  gameStatsheets)
        {
            var updates = EntityUpdate.FromArray(UpdateType.GameStatsheet, _sourceId, timestamp, gameStatsheets);
            var count = await _updateStore.SaveUpdates(conn, updates.ToList());
            _logger.Information("Saved {Count} game statsheets", count);
        }
        
        private async Task SaveTeamStatsheets(NpgsqlConnection conn, Instant timestamp, IEnumerable<JToken>  teamStatsheets)
        {
            var updates = EntityUpdate.FromArray(UpdateType.TeamStatsheet, _sourceId, timestamp, teamStatsheets);
            var count = await _updateStore.SaveUpdates(conn, updates.ToList());
            _logger.Information("Saved {Count} team statsheets", count);
        }
        
        private async Task SavePlayerStatsheets(NpgsqlConnection conn, Instant timestamp, IEnumerable<JToken> playerStatsheets)
        {
            var updates = EntityUpdate.FromArray(UpdateType.PlayerStatsheet, _sourceId, timestamp, playerStatsheets);
            var count = await _updateStore.SaveUpdates(conn, updates.ToList());
            _logger.Information("Saved {Count} player statsheets", count);
        }
        
        private async Task SaveSeasonStatsheet(NpgsqlConnection conn, Instant timestamp, JToken seasonStatsheet)
        {
            var update = EntityUpdate.From(UpdateType.SeasonStatsheet, _sourceId, timestamp, seasonStatsheet);
            await _updateStore.SaveUpdate(conn, update);
            _logger.Information("Saved season statsheet");
        }
    }
}