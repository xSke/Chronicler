using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using NodaTime;
using Serilog;
using SIBR.Storage.Data;
using SIBR.Storage.Data.Models;
using SIBR.Storage.Data.Utils;

namespace SIBR.Storage.CLI
{
    public class StreamReplay
    {
        private readonly UpdateStore _updateStore;
        private readonly Database _db;
        private readonly ILogger _logger;

        public StreamReplay(UpdateStore updateStore, Database db, ILogger logger)
        {
            _updateStore = updateStore;
            _db = db;
            _logger = logger.ForContext<StreamReplay>();
        }

        public async Task Run(ReplayOptions opts)
        {
            _logger.Information("Starting replay (type: {Type}, after: {After})", opts.Type, opts.After);
            
            using var hasher = new SibrHasher();
            var updates = _updateStore.ExportAllUpdatesRaw(UpdateType.Stream, new UpdateStore.EntityVersionQuery {
                After = opts.After
            });

            var sw = new Stopwatch();

            await using var conn = await _db.Obtain();
            await foreach (var chunk in updates.Buffer(200))
            {
                var extracted = chunk.SelectMany(streamUpdate =>
                {
                    var obj = JObject.Parse(streamUpdate.Data.GetRawText());
                    return TgbUtils.ExtractUpdatesFromStreamRoot(streamUpdate.SourceId, streamUpdate.Timestamp, obj, hasher, opts.Type).EntityUpdates;
                }).ToList();
                
                sw.Restart();
                await using var tx = await conn.BeginTransactionAsync();
                var saved = await _updateStore.SaveUpdates(conn, extracted, false);
                await tx.CommitAsync();
                sw.Stop();

                var timestamp = chunk.Min(u => u.Timestamp);
                _logger.Information("@ {Timestamp}: Saved {NewUpdateCount}/{UpdateCount} updates from {StreamObjects} stream objects (took {Duration})",
                    timestamp, saved, extracted.Count, chunk.Count, sw.Elapsed);
            }
        }

        public class ReplayOptions
        {
            public UpdateType? Type { get; set; }
            public Instant? After { get; set; }
        }
    }
}