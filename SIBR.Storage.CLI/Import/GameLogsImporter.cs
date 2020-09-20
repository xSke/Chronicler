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
    public class GameLogsImporter: S3FileImporter
    {
        private readonly Database _db;
        private readonly StreamUpdateStore _streamStore;
        private readonly GameUpdateStore _gameStore;

        public GameLogsImporter(IServiceProvider services): base(services)
        {
            FileFilter = "blaseball-log-*.json.gz";

            _db = services.GetRequiredService<Database>();
            _streamStore = services.GetRequiredService<StreamUpdateStore>();
            _gameStore = services.GetRequiredService<GameUpdateStore>();
        }
        
        protected override async Task ProcessFile(string filename, IAsyncEnumerable<JToken> entries)
        {
            var updates = new List<StreamUpdate>();
            var gameUpdates = new List<GameUpdate>();
            await foreach (var entry in entries)
            {
                var timestamp = ExtractTimestamp(entry);
                if (timestamp == null)
                    continue;
                    
                updates.Add(new StreamUpdate(timestamp.Value, WrapGamesObject(entry as JObject)));
                gameUpdates.AddRange(ExtractSchedule(entry as JObject)
                    .Select(gameUpdate => new GameUpdate(timestamp.Value, gameUpdate)));
            }

            await using var conn = await _db.Obtain();
            await using var tx = await conn.BeginTransactionAsync();
            
            var streamRes = await _streamStore.SaveUpdates(conn, updates);
            var gameRes = await _gameStore.SaveGameUpdates(conn, gameUpdates);
            _logger.Information("- Imported {StreamUpdates} stream updates ({StreamObjects} new), {GameUpdates} game updates ({GameObjects} new objects, {NewGames} new games)",
                streamRes.NewUpdates, streamRes.NewObjects, gameRes.NewUpdates, gameRes.NewObjects, gameRes.NewKeys);
            await tx.CommitAsync();
        }
        
        private IEnumerable<JObject> ExtractSchedule(JObject obj) => 
            obj["schedule"]?.OfType<JObject>() ?? new JObject[0];

        private JObject WrapGamesObject(JToken gamesObject) =>
            new JObject
            {
                {
                    "value", new JObject
                    {
                        {"games", gamesObject}
                    }
                }
            };
    }
}