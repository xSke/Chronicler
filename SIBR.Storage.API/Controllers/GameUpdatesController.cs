using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using NodaTime;
using SIBR.Storage.Data;
using SIBR.Storage.Data.Models;

namespace SIBR.Storage.API.Controllers
{
    [ApiController]
    [Route("api")]
    public class GameUpdatesController: ControllerBase
    {
        private readonly GameUpdateStore _store;

        public GameUpdatesController(GameUpdateStore store)
        {
            _store = store;
        }

        [Route("games/updates")]
        public async Task<ActionResult<IEnumerable<GameUpdate>>> GetUpdatesBySeasonDay([FromQuery] int? season, [FromQuery] int? day, [FromQuery] GameUpdatesQueryOptions opts)
        {
            if (day != null && season == null)
                return BadRequest("Can't query by only day, must also include season");
            
            var res = await _store.GetGameUpdates(new GameUpdateStore.GameUpdateQueryOptions
            {
                Season = season,
                Day = day,
                After = opts.After,
                Count = opts.Count
            });
            return Ok(res);
        }
        
        [Route("games/{game}/updates")]
        public async Task<ActionResult<IEnumerable<GameUpdate>>> GetUpdatesByGame(Guid game, [FromQuery] GameUpdatesQueryOptions opts)
        {
            var res = await _store.GetGameUpdates(new GameUpdateStore.GameUpdateQueryOptions
            {
                Game = game,
                After = opts.After,
                Count = opts.Count
            });
            return Ok(res);
        }

        public class GameUpdatesQueryOptions
        {
            public Instant? After { get; set; }
            [Range(1, 500)] public int Count { get; set; } = 100;
        }
    }
}