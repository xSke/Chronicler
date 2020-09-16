using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
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
        public async Task<ActionResult<IEnumerable<GameUpdate>>> GetUpdates([FromQuery] GameUpdateStore.GameUpdateQueryOptions opts)
        {
            var res = await _store.GetGameUpdates(opts);
            return Ok(res);
        }
    }
}