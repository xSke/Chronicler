using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using NodaTime;
using SIBR.Storage.API.Models;
using SIBR.Storage.API.Utils;
using SIBR.Storage.Data;
using SIBR.Storage.Data.Models;
using SIBR.Storage.Data.Query;

namespace SIBR.Storage.API.Controllers
{
    [ApiController]
    [Route("v{version:apiVersion}")]
    [ApiVersion("1.0")]
    public class PlayerController : ControllerBase
    {
        private readonly PlayerUpdateStore _playerUpdateStore;

        public PlayerController(PlayerUpdateStore playerUpdateStore)
        {
            _playerUpdateStore = playerUpdateStore;
        }
        
        [Route("players")]
        public async Task<IActionResult> GetPlayers([FromQuery] PlayerQuery query)
        {
            var updates = await _playerUpdateStore.GetAllPlayers();

            if (query.Forbidden != null)
                updates = updates.Where(p => p.IsForbidden == query.Forbidden.Value);

            if (query.Incinerated != null)
                updates = updates.Where(p => p.Data.GetProperty("deceased").GetBoolean() == query.Incinerated.Value);

            return Ok(new ApiResponse<ApiPlayer>
            {
                Data = updates.Select(u => new ApiPlayer(u))
            });
        }

        [Route("players/updates")]
        public async Task<IActionResult> GetPlayerUpdates([FromQuery] PlayerUpdateQuery query)
        {
            var updates = await _playerUpdateStore.GetPlayerVersions(new PlayerUpdateStore.PlayerUpdateQuery
            {
                Count = query.Count ?? 100,
                After = query.After,
                Before = query.Before,
                Players = query.Player,
                Order = query.Order,
                Page = query.Page
            }).ToListAsync();
            
            return Ok(new ApiResponsePaginated<ApiPlayerUpdate>
            {
                Data = updates.Select(u => new ApiPlayerUpdate(u)),
                NextPage = updates.LastOrDefault()?.NextPage
            });
        }

        [Route("players/names")]
        public async Task<Dictionary<string, string>> GetPlayerNames()
        {
            var players = await _playerUpdateStore.GetAllPlayerNames();
            return players.ToDictionary(p => p.PlayerId.ToString(), p => p.Name);
        }

        public class PlayerUpdateQuery: IUpdateQuery {
            [ModelBinder(BinderType = typeof(CommaSeparatedBinder))]
            public Guid[] Player { get; set; }
            public Instant? Before { get; set; }
            public Instant? After { get; set; }
            public SortOrder Order { get; set; }
            public PageToken Page { get; set; }
            [Range(1, 1000)] public int? Count { get; set; }
        }

        public class PlayerQuery
        {
            public bool? Forbidden { get; set; }
            public bool? Incinerated { get; set; }
        }
    }
}