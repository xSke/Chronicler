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
    public class TeamUpdateStore: BaseStore
    {
        public async Task<UpdateStoreResult> SaveTeamUpdates(NpgsqlConnection conn, IReadOnlyCollection<TeamUpdate> teams)
        {
            var objectRows = await SaveObjects(conn, teams);
            var rows = await conn.ExecuteAsync(
                "insert into team_updates (timestamp, hash, team_id) select unnest(@Timestamps), unnest(@Hashes), unnest(@TeamIds) on conflict do nothing", new
                {
                    Timestamps = teams.Select(to => to.Timestamp).ToArray(),
                    Hashes = teams.Select(to => to.Hash).ToArray(),
                    TeamIds = teams.Select(to => to.TeamId).ToArray()
                });

            return new UpdateStoreResult(rows, objectRows);
        }
    }
}