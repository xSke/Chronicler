using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json.Linq;
using Serilog;
using SIBR.Storage.CLI.Import;
using SIBR.Storage.Data;
using SIBR.Storage.Data.Models;

namespace SIBR.Storage.CLI
{
    public class HourlyLogsImporter : S3FileImporter
    {
        private readonly Database _db;
        private readonly TeamUpdateStore _teamStore;
        private readonly PlayerUpdateStore _playerStore;
        private readonly MiscStore _miscStore;

        public HourlyLogsImporter(IServiceProvider services) : base(services)
        {
            FileFilter = "blaseball-hourly-*.json.gz";

            _db = services.GetRequiredService<Database>();
            _teamStore = services.GetRequiredService<TeamUpdateStore>();
            _playerStore = services.GetRequiredService<PlayerUpdateStore>();
            _miscStore = services.GetRequiredService<MiscStore>();
        }

        protected override async Task ProcessFile(string filename, IAsyncEnumerable<JToken> entries)
        {
            await using var conn = await _db.Obtain();
            await using var tx = await conn.BeginTransactionAsync();

            var teams = new List<TeamUpdate>();
            var players = new List<PlayerUpdate>();
            var misc = new List<MiscUpdate>();
            await foreach (var entry in entries)
            {
                var timestamp =
                    ExtractTimestamp(entry) ??
                    ExtractTimestampFromFilename(filename, @"blaseball-hourly-(\d+)\.json\.gz");
                if (timestamp == null)
                    continue;

                var data = entry["data"];
                var endpoint = entry["endpoint"]!.Value<string>();
                switch (endpoint)
                {
                    case "allTeams":
                        teams.AddRange(ExtractTeamUpdates(timestamp.Value, data));
                        break;
                    case "players":
                        players.AddRange(ExtractPlayerUpdates(timestamp.Value, data));
                        break;
                    case "globalEvents":
                        misc.Add(new MiscUpdate(MiscUpdate.GlobalEvents, timestamp.Value, data));
                        break;
                    case "offseasonSetup":
                        misc.Add(new MiscUpdate(MiscUpdate.OffseasonSetup, timestamp.Value, data));
                        break;
                }
            }

            var teamRes = await _teamStore.SaveTeamUpdates(conn, teams);
            var playerRes = await _playerStore.SavePlayerUpdates(conn, players);
            var miscRes = await _miscStore.SaveMiscUpdates(conn, misc);
            _logger.Information(
                "- Imported {TeamUpdates} team updates ({TeamObjects} new), {PlayerUpdates} player updates ({PlayerObjects} new objects, {NewPlayers} new players), {MiscUpdates} misc updates ({MiscObjects} new)",
                teamRes.NewUpdates, teamRes.NewObjects, playerRes.NewUpdates, playerRes.NewObjects, playerRes.NewKeys, miscRes.NewUpdates,
                miscRes.NewObjects);
            await tx.CommitAsync();
        }

        private IEnumerable<PlayerUpdate> ExtractPlayerUpdates(DateTimeOffset timestamp, JToken data) =>
            data.OfType<JObject>().Select(jo => new PlayerUpdate(timestamp, jo));

        private IEnumerable<TeamUpdate> ExtractTeamUpdates(DateTimeOffset timestamp, JToken data) =>
            data.OfType<JObject>().Select(jo => new TeamUpdate(timestamp, jo));
    }
}