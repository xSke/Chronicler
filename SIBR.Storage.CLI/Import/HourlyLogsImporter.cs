using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json.Linq;
using NodaTime;
using SIBR.Storage.CLI.Import;
using SIBR.Storage.Data;
using SIBR.Storage.Data.Models;

namespace SIBR.Storage.CLI
{
    public class HourlyLogsImporter : S3FileImporter
    {
        private readonly Database _db;
        private readonly UpdateStore _updateStore;
        private readonly Guid _sourceId;

        public HourlyLogsImporter(IServiceProvider services, Guid sourceId) : base(services)
        {
            _sourceId = sourceId;
            FileFilter = "blaseball-hourly-*.json.gz";

            _db = services.GetRequiredService<Database>();
            _updateStore = services.GetRequiredService<UpdateStore>();
        }

        protected override async Task ProcessFile(string filename, IAsyncEnumerable<JToken> entries)
        {
            await using var conn = await _db.Obtain();
            await using (var tx = await conn.BeginTransactionAsync())
            {

                var updates = new List<EntityUpdate>();
                await foreach (var entry in entries)
                {
                    var timestamp =
                        ExtractTimestamp(entry) ??
                        ExtractTimestampFromFilename(filename, @"blaseball-hourly-(\d+)\.json\.gz");

                    if (timestamp != null)
                        updates.AddRange(ExtractUpdates(entry, timestamp.Value));
                }

                var res = await _updateStore.SaveUpdates(conn, updates);
                _logger.Information("- Imported {Updates} new object updates", res);
                await tx.CommitAsync();
            }

            await _updateStore.RefreshMaterializedViews(conn, "player_versions", "team_versions");
        }

        private IEnumerable<EntityUpdate> ExtractUpdates(JToken entry, Instant timestamp)
        {
            var data = entry["data"];
            var endpoint = entry["endpoint"]!.Value<string>();
            
            return endpoint switch
            {
                "allTeams" => ExtractTeamUpdates(timestamp, data),
                "players" => ExtractPlayerUpdates(timestamp, data),
                "globalEvents" => new[] {EntityUpdate.From(UpdateType.GlobalEvents, _sourceId, timestamp, data)},
                "offseasonSetup" => new[] {EntityUpdate.From(UpdateType.OffseasonSetup, _sourceId, timestamp, data)},
                _ => ImmutableList<EntityUpdate>.Empty
            };
        }

        private IEnumerable<EntityUpdate> ExtractPlayerUpdates(Instant timestamp, JToken playerUpdates) =>
            EntityUpdate.FromArray(UpdateType.Player, _sourceId, timestamp, playerUpdates);
        
        private IEnumerable<EntityUpdate> ExtractTeamUpdates(Instant timestamp, JToken teamUpdates) =>
            EntityUpdate.FromArray(UpdateType.Team, _sourceId, timestamp, teamUpdates);
    }
}