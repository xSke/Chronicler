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

namespace SIBR.Storage.API.Controllers.v2
{
    [ApiController]
    [ApiVersion("2.0")]
    [Route("v{version:apiVersion}")]
    public class UpdatesController: ControllerBase
    {
        private readonly Database _db;
        private readonly VersionStore _versionStore;

        public UpdatesController(VersionStore versionStore, Database db)
        {
            _versionStore = versionStore;
            _db = db;
        }

        [Route("updates")]
        public async Task<IActionResult> GetUpdates([Required, FromQuery] UpdateType type, [FromQuery] ApiUpdatesQuery args)
        {
            await using var conn = await _db.Obtain();
            var entities = _versionStore.GetUpdates(conn, type, args.ToDbQuery());

            var list = await entities.ToListAsync();
            return Ok(new ApiResponsePaginatedV2<ApiVersionV2>
            {
                Items = list.Select(e => new ApiVersionV2(e)),
                NextPage = list.LastOrDefault()?.NextPage
            });
        }

        public class ApiUpdatesQuery
        {
            [ModelBinder(BinderType = typeof(CommaSeparatedBinder))]
            public Guid[]? Id { get; set; }
            public Instant? Before { get; set; }
            public Instant? After { get; set; }
            [Range(1, 1000)] public int Count { get; set; } = 100;
            public SortOrder Order { get; set; }
            public PageToken Page { get; set; }

            public VersionStore.VersionQuery ToDbQuery() => new VersionStore.VersionQuery
            {
                Id = Id,
                Before = Before,
                After = After,
                Count = Count,
                Order = Order,
                Page = Page
            };
        }
    }
}