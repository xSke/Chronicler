using System;
using System.Collections.Generic;
using NodaTime;
using SIBR.Storage.Data.Models;
using SIBR.Storage.Data.Query;
using SIBR.Storage.Data.Utils;

namespace SIBR.Storage.Data
{
    public class GameStore
    {
        private readonly Database _db;
        
        public GameStore(Database db)
        {
            _db = db;
        }
        
        public IAsyncEnumerable<GameView> GetGames(GameQueryOptions opts)
        {
            var q = new SqlKata.Query("games_view");
            
            if (opts.Order == SortOrder.Desc)
                q.OrderByDesc("season", "tournament", "day");
            else
                q.OrderBy("season", "tournament", "day");

            if (opts.GameId != null) q.Where("game_id", opts.GameId.Value);
            if (opts.Season != null) q.Where("season", opts.Season.Value);
            if (opts.Tournament != null) q.Where("tournament", opts.Tournament.Value);
            if (opts.Day != null) q.Where("day", opts.Day.Value);
            if (opts.Before != null) q.Where("start_time", "<", opts.Before.Value);
            if (opts.After != null) q.Where("start_time", ">", opts.After.Value);
            if (opts.HasOutcomes != null) q.Where("has_outcomes", opts.HasOutcomes.Value);
            if (opts.HasStarted != null) q.Where("has_started", opts.HasStarted.Value);
            if (opts.HasFinished != null) q.Where("has_finished", opts.HasFinished.Value);
            if (opts.Team != null) q.Where(q => q.WhereIn("home_team", opts.Team).OrWhereIn("away_team", opts.Team));
            if (opts.Pitcher != null) q.Where(q => q.WhereIn("home_pitcher", opts.Pitcher).OrWhereIn("away_pitcher", opts.Pitcher));
            if (opts.Weather != null) q.WhereIn("weather", opts.Weather);
            if (opts.Count != null) q.Limit(opts.Count.Value);

            return _db.QueryKataAsync<GameView>(q);
        }
        
        public class GameQueryOptions
        {
            public Guid? GameId;
            public int? Tournament;
            public int? Season;
            public int? Day;
            public Instant? Before;
            public Instant? After;
            public SortOrder Order;
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