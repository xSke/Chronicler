using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Serilog;
using SIBR.Storage.Data;
using SIBR.Storage.Data.Models;

namespace SIBR.Storage.Ingest
{
    public class StreamDataConsumer: BaseWorker
    {
        private readonly EventStream _eventStream;
        private readonly Database _db;
        private readonly StreamUpdateStore _streamStore;
        private readonly GameUpdateStore _gameStore;
        private readonly ILogger _logger;
        private readonly MiscStore _miscStore;

        public StreamDataConsumer(EventStream eventStream, ILogger logger, StreamUpdateStore streamStore, Database db, GameUpdateStore gameStore, MiscStore miscStore) : base(logger)
        {
            _eventStream = eventStream;
            _logger = logger;
            _streamStore = streamStore;
            _db = db;
            _gameStore = gameStore;
            _miscStore = miscStore;
        }
        
        private async Task HandleStreamData(string obj)
        {
            var timestamp = DateTimeOffset.UtcNow;
            
            var data = JObject.Parse(obj);

            await using (var conn = await _db.Obtain())
            await using (var tx = await conn.BeginTransactionAsync())
            {
                await _streamStore.SaveUpdates(conn, new[] { new StreamUpdate(timestamp, data) });

                if (TryGetGameUpdates(data, out var gameUpdates))
                    await _gameStore.SaveGameUpdates(conn, gameUpdates.Select(update => new GameUpdate(timestamp, update)).ToArray());

                var misc = new List<MiscUpdate>();
                if (data["value"]?["games"]?["sim"] is JObject simObj)
                    misc.Add(new MiscUpdate(MiscUpdate.Sim, timestamp, simObj));
                if (data["value"]?["temporal"] is JObject temporalObj)
                    misc.Add(new MiscUpdate(MiscUpdate.Temporal, timestamp, temporalObj));
                await _miscStore.SaveMiscUpdates(conn, misc);

                await tx.CommitAsync();
            }
        }

        private bool TryGetGameUpdates(JObject input, out IEnumerable<JObject> updates)
        {
            updates = default;
            
            var schedule =  input["value"]?["games"]?["schedule"];
            if (schedule == null)
                return false;

            updates = schedule.OfType<JObject>();
            return true;
        }

        protected override async Task Run()
        {
            _logger.Information("Starting stream data consumer");
            await _eventStream.OpenStream("https://www.blaseball.com/events/streamData", async (data) =>
            {
                try
                {
                    await HandleStreamData(data);
                }
                catch (Exception e)
                {
                    _logger.Error(e, "Error while processing stream data");
                }
            });
        }
    }
}