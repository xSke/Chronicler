using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using NodaTime;
using Serilog;
using SIBR.Storage.Data;
using SIBR.Storage.Data.Models;
using SIBR.Storage.Data.Query;
using SIBR.Storage.Data.Utils;

namespace SIBR.Storage.CLI
{
    public class StreamReplay
    {
        private readonly UpdateStore _updateStore;
        private readonly GameUpdateStore _gameUpdateStore;
        private readonly Database _db;
        private readonly ILogger _logger;

        public StreamReplay(UpdateStore updateStore, Database db, ILogger logger, GameUpdateStore gameUpdateStore)
        {
            _updateStore = updateStore;
            _db = db;
            _gameUpdateStore = gameUpdateStore;
            _logger = logger.ForContext<StreamReplay>();
        }

        public async Task Run(ReplayOptions opts)
        {
            _logger.Information("Starting replay (type: {Type}, start: {Start}, end: {End})", opts.Type, opts.Start, opts.End);

            using var hasher = new SibrHasher();
            
            var sw = new Stopwatch();

            await using var conn = await _db.Obtain();

            var page = opts.Start != null ? new PageToken(opts.Start.Value, default) : null;
            while (true)
            {
                var chunk = await _updateStore.ExportAllUpdatesChunked(conn, UpdateType.Stream,
                    new UpdateStore.EntityVersionQuery
                    {
                        Page = page,
                        Before = opts.End,
                        Order = SortOrder.Asc,
                        Count = 100
                    });

                if (chunk.Count == 0)
                    break;
                page = chunk.Last().NextPage;

                if (opts.Type == UpdateType.Game)
                {
                    var extractedGameUpdates = chunk.SelectMany(streamUpdate =>
                    {
                        var obj = JObject.Parse(streamUpdate.Data.GetRawText());
                        return TgbUtils.ExtractUpdatesFromStreamRoot(streamUpdate.SourceId, streamUpdate.Timestamp, obj, hasher, opts.Type).GameUpdates;
                    }).ToList();

                    sw.Restart();
                    await using var tx = await conn.BeginTransactionAsync();
                    var savedGameUpdates = await _gameUpdateStore.SaveGameUpdates(conn, extractedGameUpdates, false);
                    await tx.CommitAsync();
                    sw.Stop();

                    var timestamp = chunk.Min(u => u.Timestamp);
                    _logger.Information("@ {Timestamp}: Saved {GameUpdateCount}/{UpdateCount} game updates from {StreamObjects} stream objects (took {Duration})",
                        timestamp, savedGameUpdates, extractedGameUpdates.Count, chunk.Count, sw.Elapsed);
                }
                else
                {
                    var extractedUpdates = chunk.SelectMany(streamUpdate =>
                    {
                        var obj = JObject.Parse(streamUpdate.Data.GetRawText());
                        return TgbUtils.ExtractUpdatesFromStreamRoot(streamUpdate.SourceId, streamUpdate.Timestamp, obj,
                            hasher, opts.Type).EntityUpdates;
                    }).ToList();
                    
                    sw.Restart();
                    await using var tx = await conn.BeginTransactionAsync();
                    var savedUpdates = await _updateStore.SaveUpdates(conn, extractedUpdates, false, append: false);
                    await tx.CommitAsync();
                    sw.Stop();

                    var timestamp = chunk.Min(u => u.Timestamp);
                    _logger.Information("@ {Timestamp}: Saved {NewUpdateCount}/{UpdateCount} updates from {StreamObjects} stream objects (took {Duration})",
                        timestamp, savedUpdates, extractedUpdates.Count, chunk.Count, sw.Elapsed);
                }
            }
        }

        public class ReplayOptions
        {
            public UpdateType? Type { get; set; }
            public Instant? Start { get; set; }
            public Instant? End { get; set; }
        }
    }
}