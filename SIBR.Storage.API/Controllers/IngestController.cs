using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json.Linq;
using NodaTime;
using Serilog;
using SIBR.Storage.Data;
using SIBR.Storage.Data.Models;

namespace SIBR.Storage.API.Controllers
{
    [Route("v{version:apiVersion}"), ApiController, ApiVersion("1.0")]
    public class IngestController : ControllerBase
    {
        private readonly IConfiguration _config;
        private readonly ILogger _logger;
        private readonly Database _db;
        private readonly UpdateStore _updateStore;
        private readonly GameUpdateStore _gameUpdateStore;

        public IngestController(IConfiguration config, Database db, UpdateStore updateStore, ILogger logger, GameUpdateStore gameUpdateStore)
        {
            _config = config;
            _db = db;
            _logger = logger.ForContext<IngestController>();
            _updateStore = updateStore;
            _gameUpdateStore = gameUpdateStore;
        }

        [HttpPost, Route("internal/updates")]
        public async Task<IActionResult> SaveUpdates([FromQuery, Required] Guid source,
            [FromBody] IEnumerable<IngestUpdate> updates)
        {
            if (!IsOnAllowedPort())
                return Unauthorized();

            var entityUpdates = updates
                .Select(u => EntityUpdate.From(u.Type, source, u.Timestamp, JToken.Parse(u.Data.GetRawText())))
                .ToList();
            
            _logger.Information("Receiving {Count} updates from source {Source}", entityUpdates.Count, source);
            
            await using var conn = await _db.Obtain();
            await using var tx = await conn.BeginTransactionAsync();
            var newUpdates = await _updateStore.SaveUpdates(conn, entityUpdates);
            await tx.CommitAsync();
            
            return Ok($"Saved {newUpdates} updates.");
        }
        
        [HttpPost, Route("internal/gameupdates")]
        public async Task<IActionResult> SaveGameUpdates([FromQuery, Required] Guid source,
            [FromBody] IEnumerable<IngestGameUpdate> updates)
        {
            if (!IsOnAllowedPort())
                return Unauthorized();

            var gameUpdates = updates
                .Select(u => GameUpdate.From(source, u.Timestamp, JToken.Parse(u.Data.GetRawText())))
                .ToList();
            
            _logger.Information("Receiving {Count} game updates from source {Source}", gameUpdates.Count, source);
            
            await using var conn = await _db.Obtain();
            await using var tx = await conn.BeginTransactionAsync();
            await _gameUpdateStore.SaveGameUpdates(conn, gameUpdates);
            await tx.CommitAsync();
            
            return Ok($"Saved {gameUpdates.Count} game updates.");
        }

        public class IngestUpdate
        {
            public UpdateType Type { get; set; }
            public Instant Timestamp { get; set; }
            public JsonElement Data { get; set; }
        }
        
        public class IngestGameUpdate
        {
            public Instant Timestamp { get; set; }
            public JsonElement Data { get; set; }
        }

        private bool IsOnAllowedPort()
        {
            var port = _config.GetValue<int>("SIBR_PRIVATE_PORT");
            return HttpContext.Connection.LocalPort == port;
        }
    }
}