using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json.Linq;
using SIBR.Storage.CLI.Import;
using SIBR.Storage.Data;
using SIBR.Storage.Data.Models;

namespace SIBR.Storage.CLI
{
    public class GameLogsImporter : S3FileImporter
    {
        private readonly Database _db;
        private readonly UpdateStore _updateStore;
        private readonly GameStore _gameStore;
        private readonly GameUpdateStore _gameUpdateStore;
        private readonly Guid _sourceId;

        public GameLogsImporter(IServiceProvider services, Guid sourceId) : base(services)
        {
            _sourceId = sourceId;
            FileFilter = "blaseball-log-*.json.gz";

            _db = services.GetRequiredService<Database>();
            _updateStore = services.GetRequiredService<UpdateStore>();
            _gameStore = services.GetRequiredService<GameStore>();
            _gameUpdateStore = services.GetRequiredService<GameUpdateStore>();
        }

        protected override async Task ProcessFile(string filename, IAsyncEnumerable<JToken> entries)
        {
            var streamUpdates = new List<EntityUpdate>();
            var miscUpdates = new List<EntityUpdate>();
            var gameUpdates = new List<GameUpdate>();
 
            await foreach (var entry in entries)
            {
                var timestamp = ExtractTimestamp(entry);
                if (timestamp == null)
                    continue;

                var root = FindStreamRoot(entry as JObject);
                streamUpdates.Add(EntityUpdate.From(UpdateType.Stream, _sourceId, timestamp.Value, root));

                if (root["value"]?["games"]?["schedule"] is JArray scheduleObj)
                    gameUpdates.AddRange(GameUpdate.FromArray(_sourceId, timestamp.Value, scheduleObj));

                if (root["value"]?["games"]?["sim"] is JObject simObj)
                    miscUpdates.Add(EntityUpdate.From(UpdateType.Sim, _sourceId, timestamp.Value, simObj));
            }

            await using var conn = await _db.Obtain();
            await using (var tx = await conn.BeginTransactionAsync())
            {
                var streamRes = await _updateStore.SaveUpdates(conn, streamUpdates, false);
                await _gameUpdateStore.SaveGameUpdates(conn, gameUpdates, false);
                var miscRes = await _updateStore.SaveUpdates(conn, miscUpdates, false);
                _logger.Information(
                    "- Imported {StreamUpdates} stream updates, {GameUpdates} game updates, {MiscUpdates} misc updates",
                    streamRes, gameUpdates.Count, miscRes);
                await tx.CommitAsync();
            }

            await _gameStore.TryAddNewGameIds(conn, gameUpdates.Select(gu => gu.GameId));
        }

        private JObject FindStreamRoot(JToken value)
        {
            // If contains "value", it's the correct stream root
            if (value is JObject root && root.ContainsKey("value"))
                return root;
            
            // Otherwise it's the games obj (eg. from iliana)
            return new JObject
            {
                {
                    "value", new JObject
                    {
                        {"games", value}
                    }
                }
            };
        }
    }
}