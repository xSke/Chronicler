using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Serilog;
using SIBR.Storage.Data;

namespace SIBR.Storage.Ingest
{
    public class StreamDataConsumer: BaseWorker
    {
        private readonly EventStream _eventStream;
        private readonly Database _db;
        private readonly StreamUpdateStore _streamStore;
        private readonly GameUpdateStore _gameStore;
        private readonly ILogger _logger;

        public StreamDataConsumer(EventStream eventStream, ILogger logger, StreamUpdateStore streamStore, Database db, GameUpdateStore gameStore) : base(logger)
        {
            _eventStream = eventStream;
            _logger = logger;
            _streamStore = streamStore;
            _db = db;
            _gameStore = gameStore;
        }
        
        private async Task HandleStreamData(string obj)
        {
            var timestamp = DateTimeOffset.UtcNow;
            
            var data = JObject.Parse(obj);

            await using (var conn = await _db.Obtain())
            await using (var tx = await conn.BeginTransactionAsync())
            {
                await _streamStore.SaveUpdate(conn, timestamp, data);

                if (TryGetGameUpdates(data, out var gameUpdates))
                {
                    await _gameStore.SaveGameUpdates(conn, timestamp, gameUpdates);
                }

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

        public override async Task Run()
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