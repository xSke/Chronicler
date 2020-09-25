using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using NodaTime;
using Npgsql;
using Serilog;
using SIBR.Storage.Data.Models;
using SIBR.Storage.Data.Utils;
using SqlKata;

namespace SIBR.Storage.Data
{
    public class GameUpdateStore
    {
        private readonly ILogger _logger;
        private readonly Database _db;
        private readonly ObjectStore _objectStore;

        public GameUpdateStore(ILogger logger, Database db, ObjectStore objectStore)
        {
            _logger = logger.ForContext<GameUpdateStore>();
            _db = db;
            _objectStore = objectStore;
        }
        
        public IAsyncEnumerable<GameUpdateView> GetGameUpdates(GameUpdateQueryOptions opts)
        {
            var q = new Query("game_updates_unique")
                .Select("game_id", "timestamp", "hash", "data")
                .OrderBy("timestamp")
                .Limit(opts.Count);

            if (opts.Season != null) q.Where("season", opts.Season.Value);
            if (opts.Day != null) q.Where("day", opts.Day.Value);
            if (opts.Game != null) q.WhereIn("game_id", opts.Game);
            if (opts.After != null) q.Where("timestamp", ">", opts.After.Value);
            if (opts.Search != null) q.WhereRaw("search_tsv @@ websearch_to_tsquery(?)", opts.Search);
            if (opts.Started != null) q.WhereRaw("(data->>'gameStart')::bool = ?", opts.Started.Value);

            return _db.QueryKataAsync<GameUpdateView>(q);
        }

        public async Task SaveGameUpdates(NpgsqlConnection conn,
            IReadOnlyCollection<GameUpdate> updates, bool log = true)
        {
            if (log)
                LogUpdates(updates);

            await _objectStore.SaveObjects(conn, updates);
            await SaveGameUpdatesTable(conn, updates);
        }

        private static async Task SaveGameUpdatesTable(NpgsqlConnection conn, IReadOnlyCollection<GameUpdate> updates)
        {
            await conn.ExecuteAsync(@"
create temporary table tmp_gameupdates as
    select
        unnest(@SourceId) as source_id,
        unnest(@Timestamp) as timestamp,
        unnest(@GameId) as game_id,
        unnest(@Hash) as hash,
        unnest(@Season) as season,
        unnest(@Day) as day;

-- Update 'raw' game updates table
insert into game_updates (source_id, timestamp, game_id, hash, season, day)
    select source_id, timestamp, game_id, hash, season, day from tmp_gameupdates
on conflict do nothing;

-- Update deduplicated/hydrated game updates table
insert into game_updates_unique (hash, game_id, timestamp, data, season, day, search_tsv)
select distinct on (hash)
    hash,
    game_id,
    timestamp,
    data,
    season,
    day,
    to_tsvector('english', data ->> 'lastUpdate')
from tmp_gameupdates
    inner join objects using (hash)
order by hash, timestamp
on conflict (hash) do update set 
    timestamp = least(game_updates_unique.timestamp, excluded.timestamp);

drop table tmp_gameupdates;
", new
            {
                SourceId = updates.Select(u => u.SourceId).ToArray(),
                Timestamp = updates.Select(u => u.Timestamp).ToArray(),
                Hash = updates.Select(u => u.Hash).ToArray(),
                GameId = updates.Select(u => u.GameId).ToArray(),
                Season = updates.Select(u => u.Season).ToArray(),
                Day = updates.Select(u => u.Day).ToArray(),
            });
        }

        private void LogUpdates(IReadOnlyCollection<GameUpdate> updates)
        {
            foreach (var update in updates)
            {
                _logger.Debug("Saving game update: {@GameUpdate}", new
                {
                    update.SourceId,
                    update.Timestamp,
                    update.GameId,
                    update.Hash
                });
            }
        }

        public class GameUpdateQueryOptions
        {
            public int? Season;
            public int? Day;
            public Guid[] Game;
            public Instant? After;
            public int Count;
            public string Search;
            public bool? Started;
        }
    }
}