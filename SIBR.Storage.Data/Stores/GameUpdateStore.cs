using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using NodaTime;
using Npgsql;
using Serilog;
using SIBR.Storage.Data.Models;
using SqlKata;
using SqlKata.Compilers;
using SqlKata.Execution;

namespace SIBR.Storage.Data
{
    public class GameUpdateStore
    {
        private readonly ILogger _logger;
        private readonly Database _db;
        private readonly ObjectStore _objectStore;

        public GameUpdateStore(ILogger logger, Database db, ObjectStore objectStore)
        {
            _logger = logger;
            _db = db;
            _objectStore = objectStore;
        }

        public async Task<IEnumerable<GameUpdate>> GetGameUpdates(GameUpdateQueryOptions opts)
        {
            await using var conn = await _db.Obtain();
            var q = new Query("game_updates_unique")
                .OrderBy("timestamp")
                .Limit(opts.Count);

            if (opts.Season != null) q.Where("season", opts.Season.Value);
            if (opts.Day != null) q.Where("day", opts.Day.Value);
            if (opts.Game != null) q.Where("game_updates.game_id", opts.Game.Value);
            if (opts.After != null) q.Where("timestamp", ">", opts.After.Value);

            var kata = new QueryFactory(conn, new PostgresCompiler());
            var res = await kata.GetAsync<GameUpdate>(q);

            return res;
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
            await conn.ExecuteAsync(@"insert into game_updates (source_id, timestamp, game_id, hash, season, day)
select unnest(@SourceId),
       unnest(@Timestamp),
       unnest(@GameId),
       unnest(@Hash),
       unnest(@Season),
       unnest(@Day)
on conflict do nothing;

insert into game_updates_unique (hash, game_id, timestamp, data, season, day, search_tsv)
select distinct on (hash) hash,
       game_id,
       timestamp,
       data,
       season,
       day,
       to_tsvector('english', data ->> 'lastUpdate')
from (select unnest(@Timestamp) as timestamp,
             unnest(@GameId)    as game_id,
             unnest(@Hash)      as hash,
             unnest(@Season)    as season,
             unnest(@Day)       as day
     ) as new_updates
         inner join objects using (hash)
order by hash, timestamp
on conflict (hash) do update set timestamp = least(game_updates_unique.timestamp, excluded.timestamp)
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
            public int? Season { get; set; }
            public int? Day { get; set; }
            public Guid? Game { get; set; }
            public Instant? After { get; set; }
            public int Count { get; set; }
        }
    }
}