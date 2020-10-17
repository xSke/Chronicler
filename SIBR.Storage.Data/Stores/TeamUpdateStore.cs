using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Dapper;
using NodaTime;
using SIBR.Storage.Data.Models;
using SIBR.Storage.Data.Query;
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

        public async Task<IEnumerable<TeamView>> GetTeams()
        {
            await using var conn = await _db.Obtain();
            return await conn.QueryAsync<TeamView>("select * from teams");
        }

        public IAsyncEnumerable<TeamUpdateView> GetTeamUpdates(TeamUpdateQueryOpts opts)
        {
            var q = new SqlKata.Query("team_versions")
                .ApplySorting(opts, "first_seen", "update_id")
                .ApplyBounds(opts, "first_seen");

            if (opts.Teams != null)
                q.WhereIn("team_id", opts.Teams);

            return _db.QueryKataAsync<TeamUpdateView>(q);
        }

        public IAsyncEnumerable<RosterUpdateView> GetRosterUpdates(RosterUpdateQueryOpts opts)
        {
            var q = new SqlKata.Query("roster_versions")
                .ApplySorting(opts, "first_seen", "update_id")
                .ApplyBounds(opts, "first_seen");

            if (opts.Teams != null)
                q.WhereIn("team_id", opts.Teams);
            if (opts.Players != null)
                q.WhereIn("player_id", opts.Players);

            return _db.QueryKataAsync<RosterUpdateView>(q);
        }

        public class TeamUpdateQueryOpts: IBoundedQuery<Instant>, IPaginatedQuery
        {
            public Guid[] Teams { get; set; }
            public Instant? Before { get; set; }
            public Instant? After { get; set; }
            public int? Count { get; set; }
            public SortOrder Order { get; set; }
            public PageToken Page { get; set; }
        }
        
        public class RosterUpdateQueryOpts: IBoundedQuery<Instant>, IPaginatedQuery
        {
            public Guid[] Teams { get; set; }
            public Guid[] Players { get; set; }
            public Instant? Before { get; set; }
            public Instant? After { get; set; }
            public int? Count { get; set; }
            public SortOrder Order { get; set; }
            public PageToken Page { get; set; }
        }
    }
}