using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using NodaTime;
using SIBR.Storage.API.Controllers.Models;
using SIBR.Storage.API.Utils;
using SIBR.Storage.Data;
using SIBR.Storage.Data.Models;

namespace SIBR.Storage.API.Controllers
{
    [ApiController]
    [Route("api/players")]
    public class PlayerController : ControllerBase
    {
        private readonly PlayerUpdateStore _playerUpdateStore;

        public PlayerController(PlayerUpdateStore playerUpdateStore)
        {
            _playerUpdateStore = playerUpdateStore;
        }
        
        [Route("")]
        public async Task<ActionResult<List<ApiPlayer>>> GetPlayers()
        {
            var updates = await _playerUpdateStore.GetAllPlayers();
            return Ok(updates.Select(ToApiPlayer));
        }

        [Route("updates")]
        public ActionResult<IAsyncEnumerable<ApiPlayerUpdate>> GetPlayerUpdates([FromQuery] PlayerUpdateQuery query)
        {
            var updates = _playerUpdateStore.GetPlayerVersions(new PlayerUpdateStore.PlayerUpdateQuery
            {
                Count = query.Count ?? 100,
                After = query.After,
                Before = query.Before,
                Players = query.Player,
                Reverse = query.Order == IUpdateQuery.ResultOrder.Desc,
                PageUpdateId = query.Page
            });
            
            return Ok(updates.Select(ToApiPlayerUpdate));
        }

        [Route("names")]
        public async Task<Dictionary<string, string>> GetPlayerNames()
        {
            var players = await _playerUpdateStore.GetAllPlayerNames();
            return players.ToDictionary(p => p.PlayerId.ToString(), p => p.Name);
        }
        
        private ApiPlayer ToApiPlayer(Player arg)
        {
            return new ApiPlayer
            {
                Id = arg.PlayerId,
                LastUpdate = arg.Timestamp,
                TeamId = arg.TeamId,
                Position = arg.Position,
                RosterIndex = arg.RosterIndex,
                Data = arg.Data
            };
        }
        
        private ApiPlayerUpdate ToApiPlayerUpdate(PlayerUpdate arg) =>
            new ApiPlayerUpdate
            {
                UpdateId = arg.UpdateId,
                PlayerId = arg.PlayerId,
                FirstSeen = arg.FirstSeen,
                LastSeen = arg.LastSeen,
                Hash = arg.Hash,
                Data = arg.Data,
            };

        public class PlayerUpdateQuery: IUpdateQuery {
            [ModelBinder(BinderType = typeof(CommaSeparatedBinder))]
            public Guid[] Player { get; set; }
            public Instant? Before { get; set; }
            public Instant? After { get; set; }
            public IUpdateQuery.ResultOrder Order { get; set; }
            public Guid? Page { get; set; }
            public int? Count { get; set; }
        }
    }
}