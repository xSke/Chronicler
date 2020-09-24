using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Dapper;
using Newtonsoft.Json.Linq;
using NodaTime;
using Npgsql;
using SIBR.Storage.Data.Models;
using SIBR.Storage.Data.Utils;
using SqlKata;

namespace SIBR.Storage.Data
{
    public class PlayerUpdateStore
    {
        private readonly Database _db;

        public PlayerUpdateStore(Database db)
        {
            _db = db;
        }

        public async Task<IEnumerable<Guid>> GetAllPlayerIds(NpgsqlConnection conn) => 
            await conn.QueryAsync<Guid>("select distinct player_id from player_versions");
        
        public async Task<IEnumerable<JObject>> GetAllPlayers()
        {
            await using var conn = await _db.Obtain();
            return await conn.QueryAsync<JObject>("select data from players");
        }
        public async Task<IEnumerable<PlayerName>> GetAllPlayerNames()
        {
            await using var conn = await _db.Obtain();
            return await conn.QueryAsync<PlayerName>("select player_id, (data->>'name') as name from players");
        }

        public IAsyncEnumerable<PlayerVersion> GetPlayerVersions(Guid? playerId, Instant? before)
        {
            var q = new Query("player_versions").OrderByDesc("first_seen");
            
            if (playerId != null)
                q.Where("player_id", playerId);
            
            if (before != null)
                q.Where("first_seen", "<", before);

            return _db.QueryKataAsync<PlayerVersion>(q);
        }

        public class PlayerName
        {
            public Guid PlayerId;
            public string Name;
        }
    }
}