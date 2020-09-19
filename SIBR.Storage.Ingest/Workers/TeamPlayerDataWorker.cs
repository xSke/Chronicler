using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Serilog;
using SIBR.Storage.Data;
using SIBR.Storage.Data.Models;

namespace SIBR.Storage.Ingest
{
    public class TeamPlayerDataWorker: IntervalWorker
    {
        private readonly HttpClient _client;
        private readonly Database _db;
        private readonly TeamUpdateStore _teamStore;
        private readonly PlayerUpdateStore _playerStore;
        
        public TeamPlayerDataWorker(ILogger logger, HttpClient client, PlayerUpdateStore playerStore, TeamUpdateStore teamStore, Database db) : base(logger)
        {
            _client = client;
            _playerStore = playerStore;
            _teamStore = teamStore;
            _db = db;
            Interval = TimeSpan.FromMinutes(5);
        }

        protected override async Task RunInterval()
        {
            await using var conn = await _db.Obtain();
            await using var tx = await conn.BeginTransactionAsync();
            
            var allTeams = (await FetchAllTeams()).ToList();
            await _teamStore.SaveTeamUpdates(conn, allTeams);

            var playerIds = allTeams.SelectMany(team => AllPlayersOnTeam(team.Data as JObject)).ToHashSet();
            playerIds.UnionWith(await _playerStore.GetAllPlayerIds(conn));

            var allPlayers = await FetchPlayersChunked(playerIds, 150);
            await _playerStore.SavePlayerUpdates(conn, allPlayers);
            
            await tx.CommitAsync();
        }

        private async Task<List<PlayerUpdate>> FetchPlayersChunked(IEnumerable<Guid> playerIds, int chunkSize)
        {
            var chunk = new List<Guid>();
            var output = new List<PlayerUpdate>();
            
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

        private async Task<IEnumerable<PlayerUpdate>> FetchPlayers(IEnumerable<Guid> playerIds)
        {
            var queryIds = string.Join(',', playerIds);
            
            var timestamp = DateTimeOffset.UtcNow;
            var json = await _client.GetStringAsync("https://www.blaseball.com/database/players?ids=" + queryIds);

            return JArray.Parse(json)
                .OfType<JObject>()
                .Select(player => new PlayerUpdate(timestamp, player));
        }

        private async Task<IEnumerable<TeamUpdate>> FetchAllTeams()
        {
            var timestamp = DateTimeOffset.UtcNow;
            var json = await _client.GetStringAsync("https://www.blaseball.com/database/allTeams");

            return JArray.Parse(json)
                .OfType<JObject>()
                .Select(team => new TeamUpdate(timestamp, team));
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