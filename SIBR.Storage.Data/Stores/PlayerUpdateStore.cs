using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Dapper;
using NodaTime;
using Npgsql;
using SIBR.Storage.Data.Models;
using SIBR.Storage.Data.Query;
using SIBR.Storage.Data.Utils;

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
        
        public async Task<IEnumerable<PlayerView>> GetAllPlayers()
        {
            await using var conn = await _db.Obtain();
            return await conn.QueryAsync<PlayerView>("select * from players");
        }
        public async Task<IEnumerable<PlayerName>> GetAllPlayerNames()
        {
            await using var conn = await _db.Obtain();
            return await conn.QueryAsync<PlayerName>("select player_id, (data->>'name') as name from players");
        }

        public IAsyncEnumerable<PlayerUpdateView> GetPlayerVersions(PlayerUpdateQuery opts)
        {
            var q = new SqlKata.Query("player_versions")
                .ApplySorting(opts, "first_seen", "update_id")
                .ApplyBounds(opts, "first_seen");
            
            if (opts.Players != null)
                q.WhereIn("player_id", opts.Players);

            return _db.QueryKataAsync<PlayerUpdateView>(q);
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

        public class PlayerUpdateQuery: IPaginatedQuery, IBoundedQuery<Instant>
        {
            public Guid[] Players { get; set; }
            public Instant? Before { get; set; }
            public Instant? After { get; set; }
            public int? Count { get; set; }
            public SortOrder Order { get; set; }
            public PageToken Page { get; set; }
        }
    }
}