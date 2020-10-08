using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json.Linq;
using NodaTime;
using SIBR.Storage.Data;
using SIBR.Storage.Data.Models;

namespace SIBR.Storage.Ingest
{
    public class TeamPlayerDataWorker : IntervalWorker
    {
        private readonly HttpClient _client;
        private readonly Database _db;
        private readonly UpdateStore _updateStore;
        private readonly PlayerUpdateStore _playerStore;
        private readonly IClock _clock;
        private readonly Guid _sourceId;

        public TeamPlayerDataWorker(IServiceProvider services, Guid sourceId) : base(services)
        {
            _sourceId = sourceId;
            _client = services.GetRequiredService<HttpClient>();
            _updateStore = services.GetRequiredService<UpdateStore>();
            _playerStore = services.GetRequiredService<PlayerUpdateStore>();
            _db = services.GetRequiredService<Database>();
            _clock = services.GetRequiredService<IClock>();
            Interval = TimeSpan.FromMinutes(2);
        }

        protected override async Task RunInterval()
        {
            await using var conn = await _db.Obtain();
            await using (var tx = await conn.BeginTransactionAsync())
            {

                var updates = new List<EntityUpdate>();
                var allTeams = (await FetchAllTeams()).ToList();
                updates.AddRange(allTeams);

                var playerIds = allTeams.SelectMany(team => AllPlayersOnTeam(team.Data as JObject)).ToHashSet();
                playerIds.UnionWith(await _playerStore.GetAllPlayerIds(conn));

                updates.AddRange(await FetchPlayersChunked(playerIds, 150));
                var res = await _updateStore.SaveUpdates(conn, updates);
                await tx.CommitAsync();
                _logger.Information("Saved {Updates} team and player updates", res);
            }

            await _db.RefreshMaterializedViews(conn, "team_versions", "player_versions", "teams", "players", "roster_versions", "current_roster");
        }

        private async Task<List<EntityUpdate>> FetchPlayersChunked(IEnumerable<Guid> playerIds, int chunkSize)
        {
            var chunk = new List<Guid>();
            var output = new List<EntityUpdate>();

            foreach (var playerId in playerIds)
            {
                chunk.Add(playerId);
                if (chunk.Count >= chunkSize)
                {
                    output.AddRange(await FetchPlayers(chunk));
                    chunk.Clear();
                }
            }

            if (chunk.Count > 0)
                output.AddRange(await FetchPlayers(chunk));

            return output;
        }

        private async Task<IEnumerable<EntityUpdate>> FetchPlayers(IEnumerable<Guid> playerIds)
        {
            var queryIds = string.Join(',', playerIds);

            var timestamp = _clock.GetCurrentInstant();
            var json = await _client.GetStringAsync("https://www.blaseball.com/database/players?ids=" + queryIds);
            return EntityUpdate.FromArray(UpdateType.Player, _sourceId, timestamp, JArray.Parse(json));
        }

        private async Task<IEnumerable<EntityUpdate>> FetchAllTeams()
        {
            var timestamp = _clock.GetCurrentInstant();
            var json = await _client.GetStringAsync("https://www.blaseball.com/database/allTeams");
            return EntityUpdate.FromArray(UpdateType.Team, _sourceId, timestamp, JArray.Parse(json));
        }

        private List<Guid> AllPlayersOnTeam(JObject teamData)
        {
            var players = new List<Guid>();
            players.AddRange(teamData["lineup"]!.Select(p => p.ToObject<Guid>()));
            players.AddRange(teamData["rotation"]!.Select(p => p.ToObject<Guid>()));
            players.AddRange(teamData["bullpen"]!.Select(p => p.ToObject<Guid>()));
            players.AddRange(teamData["bench"]!.Select(p => p.ToObject<Guid>()));
            return players;
        }
    }
}