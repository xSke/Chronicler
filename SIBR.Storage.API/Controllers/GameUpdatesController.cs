using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using NodaTime;
using SIBR.Storage.API.Utils;
using SIBR.Storage.Data;
using SIBR.Storage.Data.Models;

namespace SIBR.Storage.API.Controllers
{
    [ApiController]
    [Route("api/games/updates")]
    public class GameUpdatesController: ControllerBase
    {
        private readonly GameUpdateStore _store;

        public GameUpdatesController(GameUpdateStore store)
        {
            _store = store;
        }

        [Route("")]
        public ActionResult<IAsyncEnumerable<GameUpdateView>> GetUpdatesBySeasonDay([FromQuery] GameUpdatesQueryOptions opts)
        {
            if ((opts.Season != null) != (opts.Day != null))
                return BadRequest("Must query by both season and day");

            var res = _store.GetGameUpdates(new GameUpdateStore.GameUpdateQueryOptions
            {
                Season = opts.Season,
                Day = opts.Day,
                After = opts.After,
                Count = opts.Count,
                Game = opts.Game
            });
            return Ok(res);
        }
        
        public class GameUpdatesQueryOptions
        {
            public int? Season { get; set; }
            public int? Day { get; set; }
            
            [BindProperty(BinderType = typeof(CommaSeparatedBinder))]
            public Guid[] Game { get; set; }
            public Instant? After { get; set; }
            [Range(1, 5000)] public int Count { get; set; } = 100;
        }
    }
}