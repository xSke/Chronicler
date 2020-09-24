using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc;
using NodaTime;
using SIBR.Storage.API.Utils;
using SIBR.Storage.Data;
using SIBR.Storage.Data.Models;

namespace SIBR.Storage.API.Controllers
{
    [ApiController]
    [Route("api/games")]
    public class GamesController: ControllerBase
    {
        private readonly GameUpdateStore _store;

        public GamesController(GameUpdateStore store)
        {
            _store = store;
        }

        [Route("")]
        public IAsyncEnumerable<Game> GetGames([FromQuery] GameQueryOptions opts) => 
            _store.GetGames(new GameUpdateStore.GameQueryOptions
            {
                After = opts.After,
                Count = opts.Count,
                Season = opts.Season,
                Day = opts.Day,
                HasOutcomes = opts.Outcomes,
                HasStarted = opts.Started,
                HasFinished = opts.Finished,
                Reverse = opts.Order == GameQueryOptions.GameOrder.Desc,
                Team = opts.Team,
                Pitcher = opts.Pitcher,
                Weather = opts.Weather
            });
        
        public class GameQueryOptions
        {
            public int? Count { get; set; }
            public int? Season { get; set; }
            public int? Day { get; set; }
            public Instant? After { get; set; }
            public bool? Outcomes { get; set; }
            public GameOrder Order { get; set; } = GameOrder.Asc;
            public bool? Started { get; set; }
            public bool? Finished { get; set; }
            
            [ModelBinder(BinderType = typeof(CommaSeparatedBinder))]
            public Guid[] Team { get; set; } = null;
            
            [ModelBinder(BinderType = typeof(CommaSeparatedBinder))]
            public Guid[] Pitcher { get; set; } = null;
            
            [ModelBinder(BinderType = typeof(CommaSeparatedBinder))]
            public int[] Weather { get; set; } = null;
            
            public enum GameOrder
            {
                Asc,
                Desc
            }
        }
    }
}