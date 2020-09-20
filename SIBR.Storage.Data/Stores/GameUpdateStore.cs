using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using Newtonsoft.Json.Linq;
using Npgsql;
using NpgsqlTypes;
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
        private readonly Database _db;

        public GameUpdateStore(Database db)
        {
            _db = db;
        }

        public async Task<IEnumerable<GameUpdate>> GetGameUpdates(GameUpdateQueryOptions opts)
        {
            await using var conn = await _db.Obtain();
            var q = new Query("game_updates_view")
                .OrderBy("timestamp")
                .Limit(opts.Count);

            if (opts.Season != null) q.Where("season", opts.Season.Value);
            if (opts.Day != null) q.Where("day", opts.Day.Value);
            if (opts.Game != null) q.Where("game_id", opts.Game.Value);
            if (opts.After != null) q.Where("timestamp", ">", opts.After.Value);

            var kata = new QueryFactory(conn, new PostgresCompiler());
            var res = await kata.GetAsync<GameUpdate>(q);

            return res;
        }
        
        public async Task<UpdateStoreResult> SaveGameUpdates(NpgsqlConnection conn, IReadOnlyCollection<GameUpdate> updates)
        {
            var objectRows = await SaveObjects(conn, updates);
            var rows = await conn.ExecuteAsync(@"
insert into game_updates(timestamp, hash, game_id, data)
select timestamp, hash, game_id, (select data from objects where objects.hash = updates.hash) 
from (select unnest(@Timestamps) as timestamp, unnest(@Hashes) as hash, unnest(@GameIds) as game_id) as updates
on conflict do nothing", new
            {
                Timestamps = updates.Select(u => u.Timestamp).ToArray(),
                Hashes = updates.Select(u => u.Hash).ToArray(),
                GameIds = updates.Select(u => u.GameId).ToArray()
            });
            
            await conn.ExecuteAsync(
                "insert into games (game_id) select unnest(@GameIds) on conflict do nothing", new
                {
                    GameIds = updates.Select(u => u.GameId).ToArray()
                });
            
            return new UpdateStoreResult(rows, objectRows);
        }

        public class GameUpdateQueryOptions
        {
            public int? Season { get; set; }
            public int? Day { get; set; }
            public Guid? Game { get; set; }
            public DateTimeOffset? After { get; set; }
            public int Count { get; set; }
        }
    }
}