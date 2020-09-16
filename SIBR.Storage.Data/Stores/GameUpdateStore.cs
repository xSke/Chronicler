using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Dapper;
using Newtonsoft.Json.Linq;
using Npgsql;
using Serilog;
using SIBR.Storage.Data.Models;
using SIBR.Storage.Data.Utils;
using SqlKata;
using SqlKata.Compilers;
using SqlKata.Execution;

namespace SIBR.Storage.Data
{
    public class GameUpdateStore : BaseStore
    {
        private readonly ILogger _logger;
        private readonly Database _db;

        public GameUpdateStore(ILogger logger, Database db)
        {
            _logger = logger;
            _db = db;
        }

        public async Task<IEnumerable<GameUpdate>> GetGameUpdates(GameUpdateQueryOptions opts)
        {
            await using var conn = await _db.Obtain();
            var q = new Query("game_updates_view")
                .OrderBy("timestamp")
                .Limit(100);

            if (opts.Season != null) q.Where("season", opts.Season.Value);
            if (opts.Day != null) q.Where("day", opts.Day.Value);
            if (opts.Game != null) q.Where("game_id", opts.Game.Value);
            if (opts.After != null) q.Where("timestamp", ">", opts.After.Value);

            var kata = new QueryFactory(conn, new PostgresCompiler());
            var res = await kata.GetAsync<GameUpdate>(q);

            return res;
        }

        public async Task SaveGameUpdates(NpgsqlConnection conn, DateTimeOffset timestamp, IEnumerable<JObject> updates)
        {
            var count = 0;
            foreach (var gameUpdate in updates)
            {
                var gameId = TgbUtils.GetId(gameUpdate);
                var hash = SibrHash.HashAsGuid(gameUpdate);

                var newRow = await conn.ExecuteAsync(
                    @"
insert into game_updates (timestamp, hash, data)
select @timestamp, @hash, null
where not exists (select 1 from game_updates where hash = @hash and timestamp = @timestamp)
",
//                     @"
// insert into game_updates (timestamp, hash, data) 
// values (@timestamp, @hash, null) 
// on conflict (hash) do update 
//     set timestamp = least(game_updates.timestamp, excluded.timestamp) where game_updates.timestamp != excluded.timestamp
// returning (xmax = 0)",
                    new {timestamp, hash});

                if (newRow > 0)
                {
                    await conn.ExecuteAsync("update game_updates set data = @data where hash = @hash",
                        new {hash, data = gameUpdate});
                    count++;
                }
            }
            
            if (count > 0)
                _logger.Information("Saved {Count} game updates at {Timestamp}", count, timestamp);
        }

        public class GameUpdateQueryOptions
        {
            public int? Season { get; set; }
            public int? Day { get; set; }
            public Guid? Game { get; set; }
            public DateTimeOffset? After { get; set; }
        }
    }
}