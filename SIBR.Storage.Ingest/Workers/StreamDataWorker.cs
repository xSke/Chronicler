using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json.Linq;
using NodaTime;
using Npgsql;
using SIBR.Storage.Data;
using SIBR.Storage.Data.Models;
using SIBR.Storage.Data.Utils;

namespace SIBR.Storage.Ingest
{
    public class StreamDataWorker : BaseWorker
    {
        private readonly EventStream _eventStream;
        private readonly Database _db;
        private readonly GameUpdateStore _gameStore;
        private readonly UpdateStore _updateStore;
        private readonly IClock _clock;
        private readonly Guid _sourceId;

        public StreamDataWorker(IServiceProvider services, Guid sourceId) : base(services)
        {
            _sourceId = sourceId;
            _clock = services.GetRequiredService<IClock>();
            _updateStore = services.GetRequiredService<UpdateStore>();
            _eventStream = services.GetRequiredService<EventStream>();
            _db = services.GetRequiredService<Database>();
            _gameStore = services.GetRequiredService<GameUpdateStore>();
            _clock = services.GetRequiredService<IClock>();
        }

        private async Task HandleStreamData(string obj)
        {
            var timestamp = _clock.GetCurrentInstant();
            var data = JObject.Parse(obj);

            await using var conn = await _db.Obtain();
            await using var tx = await conn.BeginTransactionAsync();
            await _updateStore.SaveUpdate(conn, EntityUpdate.From(UpdateType.Stream, _sourceId, timestamp, data));
            
            using var hasher = new SibrHasher();
            var extracted = TgbUtils.ExtractUpdatesFromStreamRoot(_sourceId, timestamp, data, hasher);
            var gameRes = await _gameStore.SaveGameUpdates(conn, extracted.GameUpdates);
            var miscRes = await _updateStore.SaveUpdates(conn, extracted.EntityUpdates);
            
            _logger.Information("Received stream update, saved {GameUpdates} game updates, {MiscUpdates} updates", 
                gameRes, miscRes);
            
            await tx.CommitAsync();
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