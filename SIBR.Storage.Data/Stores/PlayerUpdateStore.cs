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
        
        public async Task<IEnumerable<Player>> GetAllPlayers()
        {
            await using var conn = await _db.Obtain();
            return await conn.QueryAsync<Player>("select * from players");
        }
        public async Task<IEnumerable<PlayerName>> GetAllPlayerNames()
        {
            await using var conn = await _db.Obtain();
            return await conn.QueryAsync<PlayerName>("select player_id, (data->>'name') as name from players");
        }

        public IAsyncEnumerable<PlayerUpdate> GetPlayerVersions(PlayerUpdateQuery opts)
        {
            var q = new Query("player_versions");
            if (opts.Reverse)
                q.OrderByDesc("first_seen");
            else
                q.OrderBy("first_seen");

            if (opts.Players != null) q.WhereIn("player_id", opts.Players);
            if (opts.Before != null) q.Where("first_seen", "<", opts.Before.Value);
            if (opts.After != null) q.Where("first_seen", ">", opts.After.Value);
            if (opts.Count != null) q.Limit(opts.Count.Value);

            return _db.QueryKataAsync<PlayerUpdate>(q);
        }

        public class PlayerName
        {
            public Guid PlayerId;
            public string Name;
        }

        public class PlayerInfo
        {
            public Guid PlayerId { get; set; }
        }

        public class PlayerUpdateQuery
        {
            public Guid[] Players;
            public Instant? Before;
            public Instant? After;
            public bool Reverse;
            public int? Count;
        }
    }
}