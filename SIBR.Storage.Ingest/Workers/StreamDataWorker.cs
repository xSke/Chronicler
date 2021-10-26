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
        private static readonly TimeSpan ConnectionStagger = TimeSpan.FromSeconds(1.5);
        
        private readonly EventStream _eventStream;
        private readonly Database _db;
        private readonly GameUpdateStore _gameStore;
        private readonly UpdateStore _updateStore;
        private readonly IClock _clock;
        private readonly int _streamCount;
        private readonly Guid _sourceId;

        public StreamDataWorker(IServiceProvider services, int streamCount, Guid sourceId) : base(services)
        {
            _streamCount = streamCount > 0 ? streamCount : 1;
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

            var maxPlayCount = extracted.GameUpdates.Count > 0 ? extracted.GameUpdates.Max(gu => gu.PlayCount) : -1;
            
            _logger.Information("Received stream update, saved {GameUpdates} game updates, {MiscUpdates} updates, max PC {MaxPlayCount}", 
                gameRes, miscRes, maxPlayCount);
            
            await tx.CommitAsync();
        }

        private async Task RunStreamDataConsumer(int index)
        {
            _logger.Information("Starting stream data consumer #{StreamIndex}", index);
            await _eventStream.OpenStream("https://api.blaseball.com/events/streamData", index, async (data) =>
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
        
        protected override async Task Run()
        {
            var tasks = new List<Task>();
            for (var i = 0; i < _streamCount; i++)
            {
                tasks.Add(RunStreamDataConsumer(i));
                await Task.Delay(ConnectionStagger);
            }

            await Task.WhenAll(tasks);
        }
    }
}