using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using Newtonsoft.Json.Linq;
using Npgsql;
using Serilog;
using SIBR.Storage.Data;
using SIBR.Storage.Data.Models;

namespace SIBR.Storage.CLI
{
    public class HourlyLogsImporter
    {
        private readonly ILogger _logger;
        private readonly Database _db;
        private readonly TeamUpdateStore _teamStore;
        private readonly PlayerUpdateStore _playerStore;
        private readonly MiscStore _miscStore;

        public HourlyLogsImporter(ILogger logger, Database db, TeamUpdateStore teamStore, PlayerUpdateStore playerStore, MiscStore miscStore)
        {
            _logger = logger;
            _db = db;
            _teamStore = teamStore;
            _playerStore = playerStore;
            _miscStore = miscStore;
        }

        public async Task Import(string directory)
        {
            _logger.Information("Importing hourly logs from {Directory}", directory);

            var block = new ActionBlock<string>(ProcessFile, new ExecutionDataflowBlockOptions { MaxDegreeOfParallelism = 1 });
            foreach (var file in Directory.EnumerateFiles(directory, "blaseball-hourly-*.json.gz")) 
                block.Post(file);
            
            block.Complete();
            await block.Completion;
            
            _logger.Information("Done!");
        }

        private async Task ProcessFile(string filename)
        {
            _logger.Information("Reading from {File}", filename);

            await using var conn = await _db.Obtain();
            await using var tx = await conn.BeginTransactionAsync();

            var players = new List<PlayerUpdate>();
            var misc = new List<MiscUpdate>();
            await foreach (var line in ReadJsonGzLines(filename))
            {
                var timestamp = ExtractTimestamp(filename, line);
                if (timestamp == null)
                    continue;

                var endpoint = line["endpoint"]!.Value<string>();
                if (endpoint == "allTeams")
                    await _teamStore.SaveTeamUpdates(conn, line["data"]!.OfType<JObject>().Select(jo => new TeamUpdate(timestamp.Value, jo)).ToList());
                else if (endpoint == "players")
                    players.AddRange(line["data"]!.OfType<JObject>().Select(jo => new PlayerUpdate(timestamp.Value, jo)));
                else if (endpoint == "globalEvents")
                    misc.Add(new MiscUpdate(MiscUpdate.GlobalEvents, timestamp.Value, line["data"]));
                else if (endpoint == "offseasonSetup")
                    misc.Add(new MiscUpdate(MiscUpdate.OffseasonSetup, timestamp.Value, line["data"]));
            }

            await _playerStore.SavePlayerUpdates(conn, players);
            await _miscStore.SaveMiscUpdates(conn, misc);
            await tx.CommitAsync();
        }

        private DateTimeOffset? ExtractTimestamp(string filename, JObject obj)
        {
            var timestampToken = obj["clientMeta"]?["timestamp"];
            if (timestampToken != null)
                return DateTimeOffset.FromUnixTimeMilliseconds(timestampToken.Value<long>());

            var match = Regex.Match(filename, @"blaseball-hourly-(\d+)\.json\.gz");
            if (match.Success)
                return DateTimeOffset.FromUnixTimeMilliseconds(long.Parse(match.Groups[1].Value));

            return null;
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