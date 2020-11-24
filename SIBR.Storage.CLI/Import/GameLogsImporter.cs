using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json.Linq;
using Npgsql;
using SIBR.Storage.CLI.Import;
using SIBR.Storage.Data;
using SIBR.Storage.Data.Models;
using SIBR.Storage.Data.Utils;

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
            using var hasher = new SibrHasher();
            var streamUpdates = new List<EntityUpdate>();
            var miscUpdates = new List<EntityUpdate>();
            var gameUpdates = new List<GameUpdate>();
            var gameIds = new HashSet<Guid>();

            var count = 0;

            await using var conn = await _db.Obtain();
            await foreach (var entry in entries)
            {
                var timestamp = ExtractTimestamp(entry);
                if (timestamp == null)
                    continue;

                var root = FindStreamRoot(entry as JObject);
                streamUpdates.Add(EntityUpdate.From(UpdateType.Stream, _sourceId, timestamp.Value, root));

                var updates = TgbUtils.ExtractUpdatesFromStreamRoot(_sourceId, timestamp.Value, root, hasher);
                gameUpdates.AddRange(updates.GameUpdates);
                miscUpdates.AddRange(updates.EntityUpdates);

                gameIds.UnionWith(updates.GameUpdates.Select(g => g.GameId));
                if (count++ % 100 == 0) 
                    await FlushUpdates(conn, streamUpdates, gameUpdates, miscUpdates);
            }
            
            await FlushUpdates(conn, streamUpdates, gameUpdates, miscUpdates);
            await _gameUpdateStore.UpdateSearchIndex(conn);
        }

        private async Task FlushUpdates(NpgsqlConnection conn, List<EntityUpdate> streamUpdates, List<GameUpdate> gameUpdates, List<EntityUpdate> miscUpdates)
        {
            var sw = new Stopwatch();
            sw.Start();
            await using (var tx = await conn.BeginTransactionAsync())
            {
                var streamRes = await _updateStore.SaveUpdates(conn, streamUpdates, false);
                await _gameUpdateStore.SaveGameUpdates(conn, gameUpdates, false, false);
                var miscRes = await _updateStore.SaveUpdates(conn, miscUpdates, false);
                await tx.CommitAsync();
                
                sw.Stop();

                if (streamUpdates.Count > 0)
                {
                    var currentTime = streamUpdates.Min(su => su.Timestamp);
                    _logger.Information(
                        "- @ {CurrentTime}: Imported {StreamUpdates} stream updates, {GameUpdates} game updates, {MiscUpdates} misc updates (in {Duration})",
                        currentTime, streamRes, gameUpdates.Count, miscRes, sw.Elapsed);
                }
            }

            streamUpdates.Clear();
            gameUpdates.Clear();
            miscUpdates.Clear();
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