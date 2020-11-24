using System;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
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
    public class GameUpdatesController: ControllerBase
    {
        private readonly GameUpdateStore _store;

        public GameUpdatesController(GameUpdateStore store)
        {
            _store = store;
        }

        [Route("games/updates")]
        public async Task<IActionResult> GetGameUpdates([FromQuery] GameUpdatesQueryOptions opts)
        {
            if ((opts.Season != null) != (opts.Day != null))
                return BadRequest("Must query by both season and day");

            var updates = await _store.GetGameUpdates(new GameUpdateStore.GameUpdateQueryOptions
            {
                Season = opts.Season,
                Tournament = opts.Tournament,
                Day = opts.Day,
                After = opts.After,
                Count = opts.Count ?? 100,
                Game = opts.Game,
                Order = opts.Order,
                Page = opts.Page,
                Search = opts.Search,
                Started = opts.Started
            }).ToListAsync();
            
            return Ok(new ApiResponsePaginated<ApiGameUpdate>()
            {
                 Data = updates.Select(u => new ApiGameUpdate(u)),
                 NextPage = updates.LastOrDefault()?.NextPage
            });
        }
        
        public class GameUpdatesQueryOptions: IUpdateQuery
        {
            public int? Season { get; set; }
            public int? Tournament { get; set; }
            public int? Day { get; set; }
            
            [BindProperty(BinderType = typeof(CommaSeparatedBinder))]
            public Guid[] Game { get; set; }
            public string Search { get; set; }
            public bool? Started { get; set; }
            public Instant? Before { get; set; }
            public Instant? After { get; set; }
            public SortOrder Order { get; set; }
            public PageToken Page { get; set; }
            [Range(1, 1000)] public int? Count { get; set; }
        }
    }
}