using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using Newtonsoft.Json.Linq;
using Npgsql;
using Serilog;
using SIBR.Storage.Data;
using SIBR.Storage.Data.Models;

namespace SIBR.Storage.CLI
{
    public class GameLogsImporter
    {
        private readonly ILogger _logger;
        private readonly Database _db;
        private readonly StreamUpdateStore _streamStore;
        private readonly GameUpdateStore _gameStore;

        public GameLogsImporter(ILogger logger, StreamUpdateStore streamStore, GameUpdateStore gameStore, Database db)
        {
            _logger = logger;
            _streamStore = streamStore;
            _gameStore = gameStore;
            _db = db;
        }

        public async Task Import(string directory)
        {
            _logger.Information("Importing game logs from {Directory}", directory);

            foreach (var file in Directory.EnumerateFiles(directory, "blaseball-log-*.json.gz"))
                await ProcessFile(file);

            _logger.Information("Done!");
        }

        private async Task ProcessFile(string filename)
        {
            _logger.Information("Reading from {File}", filename);
            
            var updates = new List<StreamUpdate>();
            var gameUpdates = new List<GameUpdate>();
            await foreach (var line in ReadJsonGzLines(filename))
            {
                var timestamp = ExtractTimestamp(line);
                if (timestamp == null)
                    continue;
                    
                updates.Add(new StreamUpdate(timestamp.Value, WrapGamesObject(line)));
                foreach (var gameUpdate in ExtractSchedule(line)) 
                    gameUpdates.Add(new GameUpdate(timestamp.Value, gameUpdate));
            }

            await using var conn = await _db.Obtain();
            await using var tx = await conn.BeginTransactionAsync();
            
            await _streamStore.SaveUpdates(conn, updates);
            await _gameStore.SaveGameUpdates(conn, gameUpdates);
            await tx.CommitAsync();
        }

        private DateTimeOffset? ExtractTimestamp(JObject obj)
        {
            var timestampToken = obj["clientMeta"]?["timestamp"];
            if (timestampToken == null)
                return null;

            return DateTimeOffset.FromUnixTimeMilliseconds(timestampToken.Value<long>());
        }

        private IEnumerable<JObject> ExtractSchedule(JObject obj)
        {
            if (!obj.ContainsKey("schedule"))
                return new JObject[0];
            return obj["schedule"]!.OfType<JObject>();
        }

        private JObject WrapGamesObject(JObject gamesObject)
        {
            return new JObject
            {
                {
                    "value", new JObject
                    {
                        {"games", gamesObject}
                    }
                }
            };
        }

        private async IAsyncEnumerable<JObject> ReadJsonGzLines(string file)
        {
            await using var stream = File.OpenRead(file);
            await using var gz = new GZipStream(stream, CompressionMode.Decompress);
            using var reader = new StreamReader(gz);

            string line;
            while ((line = await reader.ReadLineAsync()) != null)
            {
                var obj = JObject.Parse(line);
                yield return obj;
            }
        }
    }
}