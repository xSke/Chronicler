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

            // var gamesRes = await SaveGameUpdates(data, conn, timestamp);

            var updates = TgbUtils.ExtractUpdatesFromStreamRoot(_sourceId, timestamp, data).EntityUpdates;
            var miscRes = await _updateStore.SaveUpdates(conn, updates);
            
            _logger.Information("Received stream update, saved {MiscUpdates} updates", miscRes);
            
            await tx.CommitAsync();
        }
        
        private async Task<(int, Guid)> SaveGameUpdates(JObject data, NpgsqlConnection conn, Instant timestamp)
        {
            if (data["value"]?["games"]?["schedule"] is JArray scheduleObj)
            {
                await SaveGameUpdates(conn, timestamp, scheduleObj.OfType<JObject>());
                return (scheduleObj.Count, SibrHash.HashAsGuid(scheduleObj));
            }
            
            return (0, default);
        }

        private async Task SaveGameUpdates(NpgsqlConnection conn, Instant timestamp,
            IEnumerable<JObject> gameUpdates)
        {
            var updates = GameUpdate.FromArray(_sourceId, timestamp, gameUpdates);
            await _gameStore.SaveGameUpdates(conn, updates.ToList());
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