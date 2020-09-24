using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using NodaTime;
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
        public async Task<ActionResult<List<PlayerVersion>>> GetPlayers([FromQuery] Instant? before = null)
        {
            var updates = _playerUpdateStore.GetPlayerVersions(null, before);
            return Ok(await updates.Take(100).ToListAsync());
        }

        [Route("{playerId}")]
        public async Task<ActionResult<PlayerVersion>> GetPlayer(Guid playerId, [FromQuery] Instant? at = null)
        {
            // Add one ms because this is a before constraint
            var updates = _playerUpdateStore.GetPlayerVersions(playerId, at?.Plus(Duration.FromMilliseconds(1)));
            return Ok(await updates.FirstAsync());
        }

        [Route("{playerId}/updates")]
        public async Task<ActionResult<List<PlayerVersion>>> GetPlayerUpdates(Guid playerId,
            [FromQuery] Instant? before = null)
        {
            var updates = _playerUpdateStore.GetPlayerVersions(playerId, before);
            return Ok(await updates.Take(100).ToListAsync());
        }

        [Route("updates")]
        public async Task<ActionResult<List<PlayerVersion>>> GetAllPlayerUpdates([FromQuery] Instant? before = null)
        {
            var updates = _playerUpdateStore.GetPlayerVersions(null, before);
            return Ok(await updates.Take(100).ToListAsync());
        }

        [Route("names")]
        public async Task<Dictionary<Guid, string>> GetPlayerNames()
        {
            var players = await _playerUpdateStore.GetAllPlayerNames();
            return players.ToDictionary(p => p.PlayerId, p => p.Name);
        }
    }
}