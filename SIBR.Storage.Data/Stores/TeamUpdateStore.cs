using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Dapper;
using NodaTime;
using SIBR.Storage.Data.Models;
using SIBR.Storage.Data.Utils;
using SqlKata;

namespace SIBR.Storage.Data
{
    public class TeamUpdateStore
    {
        private readonly Database _db;

        public TeamUpdateStore(Database db)
        {
            _db = db;
        }

        public async Task<IEnumerable<Team>> GetTeams()
        {
            await using var conn = await _db.Obtain();
            return await conn.QueryAsync<Team>("select * from teams");
        }

        public IAsyncEnumerable<TeamUpdate> GetTeamUpdates(TeamUpdateQueryOpts opts)
        {
            var q = new Query("team_versions")
                .ApplyFrom(opts, "first_seen", "team_versions");

            if (opts.Teams != null)
                q.WhereIn("team_id", opts.Teams);

            return _db.QueryKataAsync<TeamUpdate>(q);
        }

        public class TeamUpdateQueryOpts: IUpdateQueryOpts
        {
            public Guid[] Teams { get; set; }
            public Instant? Before { get; set; }
            public Instant? After { get; set; }
            public int? Count { get; set; }
            public bool Reverse { get; set; }
            public Guid? PageUpdateId { get; set; }
        }
    }
}