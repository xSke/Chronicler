using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using NodaTime;
using Npgsql;
using SIBR.Storage.Data.Models;
using SIBR.Storage.Data.Utils;
using SqlKata;

namespace SIBR.Storage.Data
{
    public class PlayerUpdateStore: BaseStore
    {
        private readonly Database _db;

        public PlayerUpdateStore(IServiceProvider services): base(services)
        {
            _db = services.GetRequiredService<Database>();
        }

        public async Task<IEnumerable<Guid>> GetAllPlayerIds(NpgsqlConnection conn) => 
            await conn.QueryAsync<Guid>("select distinct player_id from player_versions");

        public IAsyncEnumerable<PlayerVersion> GetPlayerVersions(Guid? playerId, Instant? before)
        {
            var q = new Query("player_versions").OrderByDesc("first_seen");
            
            if (playerId != null)
                q.Where("player_id", playerId);
            
            if (before != null)
                q.Where("first_seen", "<", before);

            return _db.QueryKataAsync<PlayerVersion>(q);
        }
    }
}