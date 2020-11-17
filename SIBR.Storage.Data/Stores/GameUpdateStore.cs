using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using NodaTime;
using Npgsql;
using Serilog;
using SIBR.Storage.Data.Models;
using SIBR.Storage.Data.Query;
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

        public IAsyncEnumerable<GameUpdateView> GetGameUpdates(GameUpdateQueryOptions opts, bool ignoreSort = false)
        {
            var q = new SqlKata.Query("game_updates_unique")
                .Select("game_id", "timestamp", "hash", "data")
                .ApplyBounds(opts, "timestamp");
                
            if (!ignoreSort)
                q.ApplySorting(opts, "timestamp", "hash");

            if (opts.Season != null) q.Where("season", opts.Season.Value);
            if (opts.Day != null) q.Where("day", opts.Day.Value);
            if (opts.Game != null) q.WhereIn("game_id", opts.Game);
            if (opts.Search != null) q.WhereRaw("search_tsv @@ websearch_to_tsquery(?)", opts.Search);
            if (opts.Started != null) q.WhereRaw("(data->>'gameStart')::bool = ?", opts.Started.Value);

            return _db.QueryKataAsync<GameUpdateView>(q);
        }

        public async Task<int> SaveGameUpdates(NpgsqlConnection conn,
            IReadOnlyCollection<GameUpdate> updates, bool log = true, bool updateSearchIndex = true)
        {
            if (log)
                LogUpdates(updates);

            await _objectStore.SaveObjects(conn, updates);
            return await SaveGameUpdatesTable(conn, updates, updateSearchIndex);
        }

        private static async Task<int> SaveGameUpdatesTable(NpgsqlConnection conn, IReadOnlyCollection<GameUpdate> updates,
            bool updateSearchIndex)
        {
            await conn.ExecuteAsync(
                "insert into game_updates (source_id, timestamp, game_id, hash, season, day) select unnest(@SourceId), unnest(@Timestamp), unnest(@GameId), unnest(@Hash), unnest(@Season), unnest(@Day) on conflict do nothing",
                new
                {
                    SourceId = updates.Select(u => u.SourceId).ToArray(),
                    Timestamp = updates.Select(u => u.Timestamp).ToArray(),
                    Hash = updates.Select(u => u.Hash).ToArray(),
                    GameId = updates.Select(u => u.GameId).ToArray(),
                    Season = updates.Select(u => u.Season).ToArray(),
                    Day = updates.Select(u => u.Day).ToArray(),
                });

            var grouped = updates.GroupBy(u => u.Hash).ToList();
            return await conn.ExecuteAsync(@"
insert into game_updates_unique (hash, game_id, timestamp, data, season, day, search_tsv)
    select
        hash,
        game_id,
        timestamp,
        data,
        season,
        day,
        case when @UpdateSearchIndex then to_tsvector('english', data ->> 'lastUpdate') end as search_tsv
    from (select unnest(@Hash) as hash, unnest(@GameId) as game_id, unnest(@Timestamp) as timestamp, unnest(@Season) as season, unnest(@Day) as day) as new_updates
    inner join objects using (hash)
    on conflict (hash) do update set 
        timestamp = least(game_updates_unique.timestamp, excluded.timestamp);", new
            {
                Hash = grouped.Select(g => g.Key).ToArray(),
                GameId = grouped.Select(g => g.First().GameId).ToArray(),
                Timestamp = grouped.Select(g => g.Min(u => u.Timestamp)).ToArray(),
                Season = grouped.Select(g => g.First().Season).ToArray(),
                Day = grouped.Select(g => g.First().Day).ToArray(),
                UpdateSearchIndex = updateSearchIndex
            });
        }

        public async Task UpdateSearchIndex(NpgsqlConnection conn)
        {
            _logger.Information("Updating search index...");
            var sw = new Stopwatch();
            await conn.ExecuteAsync(
                "update game_updates_unique set search_tsv = to_tsvector('english', data ->> 'lastUpdate') where search_tsv is null");
            sw.Stop();
            _logger.Information("Updated search index in {Duration}", sw.Elapsed);
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

        public class GameUpdateQueryOptions: IPaginatedQuery, IBoundedQuery<Instant>
        {
            public int? Season { get; set; }
            public int? Day { get; set; }
            public Guid[] Game { get; set; }
            public Instant? Before { get; set; }
            public Instant? After { get; set; }
            public SortOrder Order { get; set; }
            public int? Count { get; set; }
            public string Search { get; set; }
            public bool? Started { get; set; }
            public PageToken Page { get; set; }
        }
    }
}