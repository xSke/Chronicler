using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using SIBR.Storage.Data;
using SIBR.Storage.Data.Models;
using SIBR.Storage.Ingest.Utils;

namespace SIBR.Storage.Ingest
{
    public class TeamElectionWorker: IntervalWorker
    {
        private readonly Database _db;
        private readonly UpdateStore _updateStore;
        private readonly HttpClient _client;
        private readonly Guid _sourceId;
        
        public TeamElectionWorker(IServiceProvider services, IntervalWorkerConfiguration config, Guid sourceId) : base(services, config)
        {
            _updateStore = services.GetRequiredService<UpdateStore>();
            _db = services.GetRequiredService<Database>();
            _client = services.GetRequiredService<HttpClient>();
            _sourceId = sourceId;
        }

        protected override async Task RunInterval()
        {
            await using var conn = await _db.Obtain();

            var teams = await _updateStore.GetLatestUpdatesFor(UpdateType.Team).ToListAsync();

            var tasks = new List<Task<EntityUpdate>>();

            var updates = await Task.WhenAll(teams
                // hack to ignore coffee teams etc
                .Where(team => team.Data.TryGetProperty("stadium", out var stadium) && stadium.ValueKind != JsonValueKind.Null)
                .Select(team => GetTeamElectionStats(team.EntityId!.Value)));

            var count = await _updateStore.SaveUpdates(conn, updates);
            _logger.Information("Saved {UpdateCount} team election stats", count);
        }

        private async Task<EntityUpdate> GetTeamElectionStats(Guid teamId)
        {
            var (timestamp, data) = await _client.GetJsonAsync($"https://www.blaseball.com/database/teamElectionStats?id={teamId}");
            return EntityUpdate.From(UpdateType.TeamElectionStats, _sourceId, timestamp, data, idOverride: teamId); 
        }
    }
}