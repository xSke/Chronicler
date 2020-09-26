using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using NodaTime;
using SIBR.Storage.API.Controllers.Models;
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
        public ActionResult<IAsyncEnumerable<GameUpdateView>> GetGameUpdates([FromQuery] GameUpdatesQueryOptions opts)
        {
            if ((opts.Season != null) != (opts.Day != null))
                return BadRequest("Must query by both season and day");

            var res = _store.GetGameUpdates(new GameUpdateStore.GameUpdateQueryOptions
            {
                Season = opts.Season,
                Day = opts.Day,
                After = opts.After,
                Count = opts.Count ?? 100,
                Game = opts.Game,
                Search = opts.Search,
                Started = opts.Started
            });
            return Ok(res);
        }
        
        public class GameUpdatesQueryOptions: IUpdateQuery
        {
            public int? Season { get; set; }
            public int? Day { get; set; }
            
            [BindProperty(BinderType = typeof(CommaSeparatedBinder))]
            public Guid[] Game { get; set; }
            public string Search { get; set; }
            public bool? Started { get; set; }
            public Instant? Before { get; set; }
            public Instant? After { get; set; }
            public IUpdateQuery.ResultOrder Order { get; set; }
            [Range(1, 1000)] public int? Count { get; set; }
        }
    }
}