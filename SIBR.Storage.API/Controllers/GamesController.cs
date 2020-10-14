using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CsvHelper;
using Microsoft.AspNetCore.Mvc;
using NodaTime;
using SIBR.Storage.API.Models;
using SIBR.Storage.API.Utils;
using SIBR.Storage.Data;
using SIBR.Storage.Data.Query;

namespace SIBR.Storage.API.Controllers
{
    [ApiController]
    [ApiVersion("1.0")]
    [Route("v{version:apiVersion}")]
    public class GamesController : ControllerBase
    {
        private readonly GameStore _store;

        public GamesController(GameStore store)
        {
            _store = store;
        }

        [Route("games")]
        public async Task<IActionResult> GetGames([FromQuery] GameQueryOptions opts)
        {
            var games = await _store.GetGames(new GameStore.GameQueryOptions
            {
                Before = opts.Before,
                After = opts.After,
                Count = opts.Count,
                Season = opts.Season,
                Day = opts.Day,
                HasOutcomes = opts.Outcomes,
                HasStarted = opts.Started,
                HasFinished = opts.Finished,
                Order = opts.Order,
                Team = opts.Team,
                Pitcher = opts.Pitcher,
                Weather = opts.Weather
            }).ToListAsync();

            if (opts.Format == ResponseFormat.Json)
            {
                return Ok(new ApiResponse<ApiGame>
                {
                    Data = games.Select(g => new ApiGame(g))
                });
            }
            else
            {
                await using var sw = new StringWriter();
                await using (var w = new CsvWriter(sw, CultureInfo.InvariantCulture))
                {
                    w.Configuration.TypeConverterOptionsCache.GetOptions<DateTimeOffset?>().Formats = new [] { "yyyy'-'MM'-'dd'T'HH':'mm':'ss'Z'" };
                    w.Configuration.TypeConverterCache.AddConverter<bool>(new LowercaseBooleanConverter());
                    await w.WriteRecordsAsync(games.Select(x => new CsvGame(x)));
                }

                return Ok(sw.ToString());
            }
        }

        public class GameQueryOptions : IUpdateQuery
        {
            public int? Season { get; set; }
            public int? Day { get; set; }
            public bool? Outcomes { get; set; }
            public bool? Started { get; set; }
            public bool? Finished { get; set; }

            [ModelBinder(BinderType = typeof(CommaSeparatedBinder))]
            public Guid[] Team { get; set; } = null;

            [ModelBinder(BinderType = typeof(CommaSeparatedBinder))]
            public Guid[] Pitcher { get; set; } = null;

            [ModelBinder(BinderType = typeof(CommaSeparatedBinder))]
            public int[] Weather { get; set; } = null;

            public Instant? Before { get; set; }
            public Instant? After { get; set; }
            public SortOrder Order { get; set; }
            public PageToken Page => null;
            public int? Count { get; set; }
            public ResponseFormat Format { get; set; }
        }
    }
}