using System;
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
    [ApiVersion("1.0")]
    [Route("v{version:apiVersion}")]
    public class FightsController : ControllerBase
    {
        private readonly UpdateStore _updateStore;

        public FightsController(UpdateStore updateStore)
        {
            _updateStore = updateStore;
        }

        [Route("fights")]
        public async Task<ActionResult<ApiResponse<ApiBasicEntity>>> GetFights()
        {
            var updates = await _updateStore.GetLatestUpdatesFor(UpdateType.Bossfight).ToListAsync();
            return Ok(new ApiResponse<ApiBasicEntity>
            {
                Data = updates.Select(u => new ApiBasicEntity(u))
            });
        }

        [Route("fights/updates")]
        public async Task<ActionResult<ApiResponsePaginated<ApiBasicEntity>>> GetFightsUpdates([FromQuery] FightUpdatesQuery query)
        {
            var updates = await _updateStore.ExportAllUpdatesRaw(UpdateType.Bossfight,new UpdateStore.EntityVersionQuery
            {
                Ids = query.Fight,
                Before = query.Before,
                After = query.After,
                Count = query.Count ?? 1000,
                Order = query.Order,
                Page = query.Page
            }).ToListAsync();
            return Ok(new ApiResponsePaginated<ApiFightUpdate>
            {
                Data = updates.Select(u => new ApiFightUpdate(u)),
                NextPage = updates.LastOrDefault()?.NextPage
            });
        }
        
        public class FightUpdatesQuery: IUpdateQuery
        {
            [BindProperty(BinderType = typeof(CommaSeparatedBinder))]
            public Guid[] Fight { get; set; }
            public Instant? Before { get; set; }
            public Instant? After { get; set; }
            public SortOrder Order { get; set; }
            public PageToken Page { get; set; }
            [Range(1, 1000)] public int? Count { get; set; }
        }
    }
}