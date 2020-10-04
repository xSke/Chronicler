using System;
using System.Collections.Generic;
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
    [ApiController]
    [Route("v{version:apiVersion}")]
    [ApiVersion("1.0")]
    public class SiteController : ControllerBase
    {
        private readonly SiteUpdateStore _store;

        public SiteController(SiteUpdateStore store)
        {
            _store = store;
        }

        [Route("site/updates")]
        public async Task<IActionResult> GetSiteUpdates([FromQuery] SiteUpdatesQueryOpts opts)
        {
            var stream = _store.GetUniqueSiteUpdates(new SiteUpdateStore.SiteUpdateQueryOpts
            {
                Count = opts.Count,
                Order = opts.Order,
                Before = opts.Before,
                After = opts.After
            });
            
            if (opts.Format != null)
                stream = stream.Where(u => u.Path.EndsWith(opts.Format));
            
            var updates = await stream.ToListAsync();
            return Ok(new ApiResponsePaginated<ApiSiteUpdate>
            {
                Data = updates.Select(u => new ApiSiteUpdate(u)),
                NextPage = updates.LastOrDefault()?.NextPage
            });
        }

        [Route("site/download/{hash}")]
        [Route("site/download/{hash}/{filename}")]
        public async Task<IActionResult> DownloadFile(Guid hash, string filename = null)
        {
            var data = await _store.GetObjectData(hash);
            if (data == null)
                return NotFound("Object with hash not found.");
            
            var contentType = filename switch
            {
                { } when filename.EndsWith(".js") => "text/javascript",
                { } when filename.EndsWith(".css") => "text/css",
                { } when filename.EndsWith(".html") => "text/html",
                _ => "application/octet-stream"
            };

            return File(data, contentType, filename);
        }

        public class SiteUpdatesQueryOpts: IUpdateQuery
        {
            public string Format { get; set; }
            public Instant? Before { get; set; }
            public Instant? After { get; set; }
            public PageToken Page { get; set; }
            public int? Count { get; set; }
            public SortOrder Order { get; set; }
        }
    }
}