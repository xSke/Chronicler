using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using Newtonsoft.Json.Linq;
using Npgsql;
using Serilog;
using SIBR.Storage.Data.Models;

namespace SIBR.Storage.Data
{
    public class PlayerUpdateStore: BaseStore
    {
        public async Task<UpdateStoreResult> SavePlayerUpdates(NpgsqlConnection conn, IReadOnlyCollection<PlayerUpdate> players)
        {
            var objectRows = await SaveObjects(conn, players);
            var rows = await conn.ExecuteAsync(
                "insert into player_updates (timestamp, hash, player_id) select unnest(@Timestamps), unnest(@Hashes), unnest(@PlayerIds) on conflict do nothing", new
                {
                    Timestamps = players.Select(po => po.Timestamp).ToArray(),
                    Hashes = players.Select(po => po.Hash).ToArray(),
                    PlayerIds = players.Select(po => po.PlayerId).ToArray()
                });
            
            var newPlayers = await conn.ExecuteAsync(
                "insert into players (player_id) select unnest(@PlayerIds) on conflict do nothing", new
                {
                    PlayerIds = players.Select(po => po.PlayerId).ToArray()
                });
            
            return new UpdateStoreResult(rows, objectRows, newPlayers);
        }

        public async Task<IEnumerable<Guid>> GetAllPlayerIds(NpgsqlConnection conn) => 
            await conn.QueryAsync<Guid>("select player_id from players");
    }
}