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
            {
                _logger.Information("Reading from {File}", file);
                
                var updates = ReadJsonGzLines(file)
                    .Where(obj => ExtractTimestamp(obj) != null)
                    .Select(obj => (ExtractTimestamp(obj)!.Value, WrapGamesObject(obj)));
                await _streamStore.SaveUpdatesBulk(updates);
            }
            
            _logger.Information("Done!");
        }

        private DateTimeOffset? ExtractTimestamp(JObject obj)
        {
            var timestampToken = obj["clientMeta"]?["timestamp"];
            if (timestampToken == null)
                return null;

            return DateTimeOffset.FromUnixTimeMilliseconds(timestampToken.Value<long>());
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

        /*private async Task InsertObject(NpgsqlConnection conn, JObject gamesObject)
        {


            var rootObject = 
            await _streamStore.SaveUpdate(conn, timestamp, rootObject);

            if (gamesObject.ContainsKey("schedule"))
            {
                var games = gamesObject["schedule"]!.Value<JArray>().OfType<JObject>();
                await _gameStore.SaveGameUpdates(conn, timestamp, games);
            }
            else
            {
                _logger.Warning("Update at {Timestamp} does not have schedules key, skipping", timestamp);
            }
        }*/

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