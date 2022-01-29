using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using NodaTime;
using SIBR.Storage.API.Models;
using SIBR.Storage.API.Utils;
using SIBR.Storage.Data;
using SIBR.Storage.Data.Query;

namespace SIBR.Storage.API.Controllers.v2
{
    [ApiController]
    [ApiVersion("2.0")]
    [Route("v{version:apiVersion}")]
    public class EventsController: ControllerBase
    {
        private readonly Database _db;
        private readonly PusherEventStore _eventStore;

        public EventsController(Database db, PusherEventStore eventStore)
        {
            _db = db;
            _eventStore = eventStore;
        }

        [Route("events")]
        public async Task<IActionResult> GetEvents([FromQuery] ApiEventQuery args)
        {
            await using var conn = await _db.Obtain();
            var events = _eventStore.GetEvents(conn, args.ToDbQuery());
            
            var list = await events.ToListAsync();

            // TODO: when migrating to .NET 5, unify Raw and normal types and just add JsonIgnore condition
            if (args.Raw)
            {
                return Ok(new ApiResponseV2<ApiPusherEventRaw>
                {
                    Items = list.Select(e => new ApiPusherEventRaw(e)),
                });
            }

            return Ok(new ApiResponseV2<ApiPusherEvent>
            {
                Items = list.Select(e => new ApiPusherEvent(e)),
            });
        }

        public class ApiEventQuery
        {
            [ModelBinder(BinderType = typeof(CommaSeparatedBinder))]
            public string[]? Channel { get; set; }
            
            [ModelBinder(BinderType = typeof(CommaSeparatedBinder))]
            public string[]? Event { get; set; }

            public Instant? Before { get; set; }
            public Instant? After { get; set; }
            [Range(1, 1000)] public int Count { get; set; } = 100;
            public SortOrder Order { get; set; }

            public bool Raw { get; set; } = false;
            
            public PusherEventStore.EventQuery ToDbQuery() => new PusherEventStore.EventQuery
            {
                Channel = Channel,
                Event = Event,
                Before = Before,
                After = After,
                Count = Count,
                Order = Order
            };

        }
    }
}