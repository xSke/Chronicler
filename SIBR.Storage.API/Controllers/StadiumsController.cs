using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using SIBR.Storage.API.Models;
using SIBR.Storage.Data;
using SIBR.Storage.Data.Models;

namespace SIBR.Storage.API.Controllers
{
    [ApiController]
    [Route("v{version:apiVersion}")]
    [ApiVersion("1.0")]
    public class StadiumsController: ControllerBase
    { 
        private readonly UpdateStore _updateStore;

        public StadiumsController(UpdateStore updateStore)
        {
            _updateStore = updateStore;
        }

        [Route("stadiums")]
        public async Task<IActionResult> GetStadiums()
        {
            var stadiums = await _updateStore.GetLatestUpdatesFor(UpdateType.Stadium).ToListAsync();
            return Ok(new ApiResponse<ApiBasicEntity>
            {
                Data = stadiums.Select(view => new ApiBasicEntity(view))
            });
        }
    }
}