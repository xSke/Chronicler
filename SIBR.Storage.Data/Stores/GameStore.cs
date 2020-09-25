using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NodaTime;
using Npgsql;
using SIBR.Storage.Data.Models;
using SIBR.Storage.Data.Utils;
using SqlKata;

namespace SIBR.Storage.Data
{
    public class GameStore
    {
        private readonly Database _db;
        private readonly HashSet<Guid> _knownGames = new HashSet<Guid>(); 
        
        public GameStore(Database db)
        {
            _db = db;
        }
        
        public IAsyncEnumerable<Game> GetGames(GameQueryOptions opts)
        {
            var q = new Query("games_view");
            
            if (opts.Reverse)
                q.OrderByDesc("season", "day");
            else
                q.OrderBy("season", "day");

            if (opts.Season != null) q.Where("season", opts.Season.Value);
            if (opts.Day != null) q.Where("day", opts.Day.Value);
            if (opts.After != null) q.Where("start_time", ">", opts.After.Value);
            if (opts.HasOutcomes != null) q.Where("has_outcomes", opts.HasOutcomes.Value);
            if (opts.HasStarted != null) q.Where("has_started", opts.HasStarted.Value);
            if (opts.HasFinished != null) q.Where("has_finished", opts.HasFinished.Value);
            if (opts.Team != null) q.Where(q => q.WhereIn("home_team", opts.Team).OrWhereIn("away_team", opts.Team));
            if (opts.Pitcher != null) q.Where(q => q.WhereIn("home_pitcher", opts.Pitcher).OrWhereIn("away_pitcher", opts.Pitcher));
            if (opts.Weather != null) q.WhereIn("weather", opts.Weather);
            if (opts.Count != null) q.Limit(opts.Count.Value);

            return _db.QueryKataAsync<Game>(q);
        }

        public async Task TryAddNewGameIds(NpgsqlConnection conn, IEnumerable<Guid> gameIds)
        {
            var anyNewGames = false;
            foreach (var gameId in gameIds)
                if (_knownGames.Add(gameId))
                    anyNewGames = true;

            if (anyNewGames)
                await _db.RefreshMaterializedViews(conn, "games");
        }
        
        public class GameQueryOptions
        {
            public int? Season;
            public int? Day;
            public Instant? After;
            public bool Reverse;
            public int? Count;
            public bool? HasOutcomes;
            public bool? HasFinished;
            public bool? HasStarted;
            public Guid[] Team;
            public Guid[] Pitcher;
            public int[] Weather;
        }
    }
}