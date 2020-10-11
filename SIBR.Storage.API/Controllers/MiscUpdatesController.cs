using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using NodaTime;
using SIBR.Storage.API.Models;
using SIBR.Storage.Data;
using SIBR.Storage.Data.Models;
using SIBR.Storage.Data.Query;

namespace SIBR.Storage.API.Controllers
{
    [Route("v{version:apiVersion}"), ApiController]
    [ApiVersion("1.0")]
    public class MiscUpdatesController: ControllerBase
    {
        private readonly UpdateStore _updateStore;

        public MiscUpdatesController(UpdateStore updateStore)
        {
            _updateStore = updateStore;
        }

        [Route("temporal/updates")]
        public Task<ActionResult<ApiResponsePaginated<ApiBasicVersion>>> GetTemporalUpdates([FromQuery] BasicUpdateQuery q) => 
            BasicVersionHandler(UpdateType.Temporal, "temporal_versions", q, 500);

        [Route("globalevents/updates")]
        public Task<ActionResult<ApiResponsePaginated<ApiBasicVersion>>> GetGlobalEventsUpdates([FromQuery] BasicUpdateQuery q) =>
            BasicVersionHandler(UpdateType.GlobalEvents, "globalevents_versions", q, 500);
        
        [Route("sim/updates")]
        public Task<ActionResult<ApiResponsePaginated<ApiBasicVersion>>> GetSimDataUpdates([FromQuery] BasicUpdateQuery q) =>
            BasicVersionHandler(UpdateType.Sim, "simdata_versions", q, 500);
        
        [Route("stream/updates")]
        public Task<ActionResult<ApiResponsePaginated<ApiBasicVersion>>> GetStreamUpdates([FromQuery] BasicUpdateQuery q) =>
            BasicUpdateHandler(UpdateType.Stream, q, 100);

        private async Task<ActionResult<ApiResponsePaginated<ApiBasicVersion>>> BasicVersionHandler(UpdateType type,
            string table, IUpdateQuery q, int defaultCount)
        {
            var versions = await _updateStore
                .GetAllVersions(type, table, ToDbQuery(q, defaultCount))
                .ToListAsync();
            
            return Ok(new ApiResponsePaginated<ApiBasicVersion>
            {
                Data = versions
                    .Select(v => new ApiBasicVersion(v)),
                NextPage = versions.LastOrDefault()?.NextPage
            });
        }
        
        private async Task<ActionResult<ApiResponsePaginated<ApiBasicVersion>>> BasicUpdateHandler(UpdateType type,
            IUpdateQuery q, int defaultCount)
        {
            var versions = await _updateStore
                .ExportAllUpdatesRaw(type, ToDbQuery(q, defaultCount))
                .ToListAsync();
            
            return Ok(new ApiResponsePaginated<ApiBasicUpdate>
            {
                Data = versions
                    .Select(v => new ApiBasicUpdate(v)),
                NextPage = versions.LastOrDefault()?.NextPage
            });
        }
        
        private UpdateStore.EntityVersionQuery ToDbQuery(IUpdateQuery q, int? defaultCount) => new UpdateStore.EntityVersionQuery
        {
            Before = q.Before,
            After = q.After,
            Order = q.Order,
            Page = q.Page,
            Count = q.Count ?? defaultCount
        };

        public class BasicUpdateQuery: IUpdateQuery
        {
            public Instant? Before { get; set; }
            public Instant? After { get; set; }
            public SortOrder Order { get; set; }
            public PageToken Page { get; set; }
            [Range(1, 1000)] public int? Count { get; set; }
        }
    }
}