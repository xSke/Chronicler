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
        private Task _refreshMatviewTask;

        public TeamPlayerDataWorker(IServiceProvider services, IntervalWorkerConfiguration config, Guid sourceId) : base(services, config)
        {
            _sourceId = sourceId;
            _client = services.GetRequiredService<HttpClient>();
            _updateStore = services.GetRequiredService<UpdateStore>();
            _playerStore = services.GetRequiredService<PlayerUpdateStore>();
            _db = services.GetRequiredService<Database>();
            _clock = services.GetRequiredService<IClock>();
        }

        protected override async Task RunInterval()
        {
            await using var conn = await _db.Obtain();

            List<EntityUpdate> teamUpdates;
            List<EntityUpdate> playerUpdates;
            
            await using (var tx = await conn.BeginTransactionAsync())
            {
                teamUpdates = (await FetchAllTeams()).ToList();

                var count = await _updateStore.SaveUpdates(conn, teamUpdates);
                await tx.CommitAsync();
                _logger.Information("Saved {Updates} team updates", count);
            }

            await using (var tx = await conn.BeginTransactionAsync())
            {
                var playerIds = teamUpdates.SelectMany(team => AllPlayersOnTeam(team.Data as JObject)).ToHashSet();
                playerIds.UnionWith(await _playerStore.GetAllPlayerIds(conn));
                
                playerUpdates = await FetchPlayersChunked(playerIds, 150);
                var count = await _updateStore.SaveUpdates(conn, playerUpdates);
                await tx.CommitAsync();
                _logger.Information("Saved {Updates} player updates", count);
            }

            await using (var tx = await conn.BeginTransactionAsync())
            {
                var items = ExtractItems(playerUpdates);
                
                var count = await _updateStore.SaveUpdates(conn, items);
                await tx.CommitAsync();
                _logger.Information("Saved {Updates} item updates", count);
            }

            ForkRefreshMatviews();
        }

        private void ForkRefreshMatviews()
        {
            async Task Inner()
            {
                try
                {
                    await using var conn = await _db.Obtain();
                    await _db.RefreshMaterializedViews(conn, "team_versions", "player_versions", "teams", "players",
                        "roster_versions", "current_roster");
                }
                catch (Exception e)
                {
                    _logger.Error(e, "Error refreshing team/player matviews");
                }
            }

            if (_refreshMatviewTask == null || _refreshMatviewTask.IsCompleted)
                _refreshMatviewTask = Inner();
            else
                _logger.Warning("Matview refresh still running, skipping");
        }

        private List<EntityUpdate> ExtractItems(List<EntityUpdate> playerUpdates)
        {
            return playerUpdates.SelectMany(update =>
            {
                return update.Data.SelectTokens("items[*]")
                    .Select(data => EntityUpdate.From(UpdateType.Item, update.SourceId, update.Timestamp, data));
            }).ToList();
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