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
        
        public async Task SaveGameUpdates(NpgsqlConnection conn, IReadOnlyCollection<GameUpdate> updates)
        {
            var objectRows = await SaveObjects(conn, updates);
            var rows = await conn.ExecuteAsync("insert into game_updates(timestamp, hash, game_id) select unnest(@Timestamps), unnest(@Hashes), unnest(@GameIds) on conflict do nothing", new
            {
                Timestamps = updates.Select(u => u.Timestamp).ToArray(),
                Hashes = updates.Select(u => u.Hash).ToArray(),
                GameIds = updates.Select(u => u.GameId).ToArray()
            });
            await conn.ExecuteAsync("update game_updates set search_tsv = (select to_tsvector('english', data->>'lastUpdate') from objects where objects.hash = game_updates.hash) where search_tsv is null");
            if (rows > 0) 
                _logger.Information("Saved {RowCount} game updates, {ObjectRowCount} new objects", rows, objectRows);
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