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
    public class EntitiesController: ControllerBase
    {
        private readonly Database _db;
        private readonly VersionStore _versionStore;

        public EntitiesController(VersionStore versionStore, Database db)
        {
            _versionStore = versionStore;
            _db = db;
        }
        
        [Route("entities")]
        public async Task<IActionResult> GetEntities([Required, FromQuery] UpdateType type, [FromQuery] ApiEntityQuery args)
        {
            if (args.Count == null)
                args.Count = type == UpdateType.Player ? 2000 : 1000;
            
            await using var conn = await _db.Obtain();
            var entities = _versionStore.GetEntities(conn, type, args.ToDbQuery());
            
            var list = await entities.ToListAsync();
            return Ok(new ApiResponsePaginatedV2<ApiVersionV2> 
            {
                Items = list.Select(e => new ApiVersionV2(e)),
                NextPage = list.LastOrDefault()?.NextPage
            });
        }

        public class ApiEntityQuery
        {
            [ModelBinder(BinderType = typeof(CommaSeparatedBinder))]
            public Guid[]? Id { get; set; }
            public Instant? At { get; set; }
            [Range(1, 2000)] public int? Count { get; set; }
            public PageToken Page { get; set; }
            
            public VersionStore.EntityQuery ToDbQuery() => new VersionStore.EntityQuery
            {
                Id = Id,
                At = At,
                Count = Count,
                Page = Page
            };
        }
    }
}