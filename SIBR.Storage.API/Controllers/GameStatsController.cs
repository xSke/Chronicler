using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CsvHelper;
using Microsoft.AspNetCore.Mvc;
using NodaTime;
using Serilog;
using SIBR.Storage.API.Models;
using SIBR.Storage.API.Utils;
using SIBR.Storage.Data;
using SIBR.Storage.Data.Models;
using SIBR.Storage.Data.Query;

namespace SIBR.Storage.API.Controllers
{
    [ApiController]
    [ApiVersion("1.0")]
    [Route("v{version:apiVersion}")]
    public class GameStatsController : ControllerBase
    {
        private readonly GameStore _gameStore;
        private readonly UpdateStore _store;

        public GameStatsController(GameStore gameStore, UpdateStore store)
        {
            _gameStore = gameStore;
            _store = store;
        }

        [Route("games/stats")]
        public async Task<IActionResult> GetGameStats([FromQuery] GameStatsOptions opts)
        {
            var game = await _gameStore.GetGames(new GameStore.GameQueryOptions { GameId = opts.Game, Count = 1 }).FirstAsync();
            var cutoff = CutoffTime(game.EndTime);

            var gameStats = await GetVersion(UpdateType.GameStatsheet, new [] { game.Statsheet }, cutoff).FirstOrDefaultAsync();
            if (gameStats is null)
                return Ok(new ApiResponse<ApiGameStats>() { Data = new ApiGameStats[] {} });

            var teamSheets = new [] { gameStats.Data.GetProperty("awayTeamStats").GetGuid(), gameStats.Data.GetProperty("homeTeamStats").GetGuid() };
            var teamStats = await GetVersion(UpdateType.TeamStatsheet, teamSheets, cutoff).ToListAsync();

            var playerSheets = teamStats.SelectMany(sheet => sheet.Data.GetProperty("playerStats").EnumerateArray().Select(el => el.GetGuid()));
            var playerStats = await GetVersion(UpdateType.PlayerStatsheet, playerSheets.ToArray(), cutoff).ToListAsync();

            return Ok(new ApiResponse<ApiGameStats>() { Data = new ApiGameStats[]
            {
                new ApiGameStats
                {
                    GameId = game.GameId,
                    Timestamp = gameStats.Timestamp,
                    GameStats = gameStats.Data,
                    TeamStats = teamStats.Select(v => v.Data).ToArray(),
                    PlayerStats = playerStats.Select(v => v.Data).ToArray(),
                }
            }});
        }

        private Instant? CutoffTime(Instant? time)
        {
            if (time is null)
                return null;

            // We want the last recorded statsheet before the next game begins, so take the end time and round up to the next hour.
            var dt = (time ?? Instant.MaxValue).InUtc();
            var cutoff = dt.Date + TimeAdjusters.TruncateToHour(dt.TimeOfDay);
            return cutoff.InUtc().ToInstant() + Duration.FromHours(1);
        }

        private IAsyncEnumerable<EntityUpdateView> GetVersion(UpdateType type, Guid[] ids, Instant? before)
        {
            return _store.ExportAllUpdatesRaw(type, new UpdateStore.EntityVersionQuery
            {
                Ids = ids,
                Before = before,
                Order = SortOrder.Desc,
                Count = ids.Length,
            });
        }

        public class GameStatsOptions
        {
            public Guid Game { get; set; }
        }
    }
}
