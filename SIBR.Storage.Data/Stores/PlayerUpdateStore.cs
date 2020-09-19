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
        private readonly ILogger _logger;

        public PlayerUpdateStore(ILogger logger)
        {
            _logger = logger;
        }

        public async Task SavePlayerUpdates(NpgsqlConnection conn, IReadOnlyCollection<PlayerUpdate> players)
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
            
            if (rows > 0)
                _logger.Information("Imported {RowCount} player updates, {ObjectRowCount} new objects, {NewPlayers} new players", rows, objectRows, newPlayers);
        }

        public async Task<IEnumerable<Guid>> GetAllPlayerIds(NpgsqlConnection conn) => 
            await conn.QueryAsync<Guid>("select player_id from players");
    }
}