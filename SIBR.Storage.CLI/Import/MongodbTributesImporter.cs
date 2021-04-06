using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json.Linq;
using NodaTime.Extensions;
using SIBR.Storage.Data;
using SIBR.Storage.Data.Models;

namespace SIBR.Storage.CLI.Import
{
    public class MongodbTributesImporter: S3FileImporter
    {
        private readonly Guid _sourceId;
        private readonly Database _db;
        private readonly UpdateStore _store;

        public MongodbTributesImporter(IServiceProvider services, Guid sourceId) : base(services)
        {
            FileFilter = "tributes.dump.json.gz";

            _sourceId = sourceId;
            _db = services.GetRequiredService<Database>();
            _store = services.GetRequiredService<UpdateStore>();
        }

        protected override async Task ProcessFile(string filename, IAsyncEnumerable<JToken> entries)
        {
            await using var conn = await _db.Obtain();
            await using var tx = await conn.BeginTransactionAsync();
            await foreach (var entry in entries)
            {
                var timestamp = entry["firstSeen"]["$date"].ToObject<DateTime>().ToInstant();
                var data = entry["payload"];
                await _store.SaveUpdate(conn, EntityUpdate.From(UpdateType.Tributes, _sourceId, timestamp, data), false);
                _logger.Information("Saved tributes update at {Timestamp}", timestamp);
            }

            await tx.CommitAsync();
        }
    }
}