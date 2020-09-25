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
    [Route("api/site")]
    public class SiteController : ControllerBase
    {
        private readonly SiteUpdateStore _store;

        public SiteController(SiteUpdateStore store)
        {
            _store = store;
        }

        [Route("updates")]
        public async Task<ActionResult<IEnumerable<ApiSiteUpdate>>> GetSiteUpdates(string format = null)
        {
            var stream = _store.GetUniqueSiteUpdates();
            if (format != null)
                stream = stream.Where(u => u.Path.EndsWith(format));

            return Ok(await stream.Select(MapToApiUpdate).ToListAsync());
        }

        [Route("download/{hash}")]
        [Route("download/{hash}/{filename}")]
        public async Task<ActionResult> DownloadFile(Guid hash, string filename = null)
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

        public class ApiSiteUpdate
        {
            public Instant Timestamp { get; set; }
            public string Path { get; set; }
            public Guid Hash { get; set; }
            public int Size { get; set; }
            public string Download { get; set; }
        }
        
        private ApiSiteUpdate MapToApiUpdate(SiteUpdateUnique update)
        {
            var filename = update.Path.Split("/").Last();
            if (string.IsNullOrWhiteSpace(filename))
                filename = "index.html";

            return new ApiSiteUpdate
            {
                Timestamp = update.Timestamp,
                Hash = update.Hash,
                Path = update.Path,
                Download = $"/api/site/download/{update.Hash}/{filename}",
                Size = update.Size
            };
        }
    }
}