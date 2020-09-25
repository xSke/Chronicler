using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.Dynamic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CsvHelper;
using CsvHelper.Configuration;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json.Linq;
using NodaTime;
using Serilog;
using SIBR.Storage.Data;
using SIBR.Storage.Data.Models;

namespace SIBR.Storage.API.Controllers
{
    [Route("api/tributes")]
    [ApiController]
    public class TributesController : ControllerBase
    {
        private readonly IdolsTributesStore _store;
        private readonly ILogger _logger;

        public TributesController(IdolsTributesStore store, ILogger logger)
        {
            _store = store;
            _logger = logger.ForContext<TributesController>();
        }

        [Route("updates")]
        public async Task<IActionResult> GetTributes([FromQuery] TributeQueryOptions opts)
        {
            var updates = await _store.GetTributeUpdates(opts.Before, opts.Count, opts.Players);
            return CreateResponse(opts, updates);
        }

        [Route("hourly")]
        public async Task<IActionResult> GetTributesHourly([FromQuery] TributeQueryOptions opts)
        {
            var updates = await _store.GetTributeUpdatesHourly(opts.Before, opts.Count, opts.Players);
            return CreateResponse(opts, updates);
        }

        private OkObjectResult CreateResponse(TributeQueryOptions opts, IEnumerable<TributesUpdate> updates)
        {
            return opts.Format switch
            {
                TributeOutputFormat.Json => Ok(updates.Select(MapToApiTributes)),
                TributeOutputFormat.Csv => Ok(ToCsvString(updates))
            };
        }

        private string ToCsvString(IEnumerable<TributesUpdate> updates)
        {
            using var stream = new StringWriter();
            using var csv = new CsvWriter(stream, CultureInfo.InvariantCulture);

            var playerFieldOrder = new List<Guid>();
            foreach (var update in updates)
            {
                if (playerFieldOrder.Count == 0)
                {
                    // Set up indices based on first row
                    playerFieldOrder.AddRange(update.Players);
                    
                    // Write headers
                    csv.WriteField("timestamp");
                    foreach (var player in update.Players) 
                        csv.WriteField(player.ToString());
                    csv.NextRecord();
                }
                
                // Write each row
                var peanutsByPlayer = update.Players
                    .Zip(update.Peanuts)
                    .ToDictionary(k => k.First, v => v.Second);
                
                csv.WriteField(update.Timestamp);
                foreach (var player in playerFieldOrder)
                {
                    if (peanutsByPlayer.TryGetValue(player, out var peanuts))
                        csv.WriteField(peanuts);
                    else
                        csv.WriteField(null);
                }
                    
                csv.NextRecord();
            }
            
            return stream.ToString();
        }

        private ApiTributes MapToApiTributes(TributesUpdate tributes)
        {
            var players = new Dictionary<string, int>();
            for (var i = 0; i < tributes.Players.Length; i++)
                players[tributes.Players[i].ToString()] = tributes.Peanuts[i];

            return new ApiTributes
            {
                Timestamp = tributes.Timestamp,
                Players = players
            };
        }

        public class ApiTributes
        {
            public Instant Timestamp { get; set; }
            public Dictionary<string, int> Players { get; set; }
        }

        public class TributeQueryOptions
        {
            public Instant Before { get; set; } = Instant.MaxValue;
            [Range(1, 1000)] public int Count { get; set; } = 250;
            public Guid[] Players { get; set; } = null;
            public TributeOutputFormat Format { get; set; } = TributeOutputFormat.Json;
        }

        public enum TributeOutputFormat
        {
            Json,
            Csv
        }
    }
}