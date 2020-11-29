using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json.Linq;
using SIBR.Storage.Data;
using SIBR.Storage.Data.Models;

namespace SIBR.Storage.CLI.Import
{
    public class IdolLogsImporter: S3FileImporter
    {
        private readonly Database _db;
        private readonly UpdateStore _store;
        private readonly Guid _sourceId;
        
        public IdolLogsImporter(IServiceProvider services, Guid sourceId) : base(services)
        {
            _sourceId = sourceId;
            FileFilter = "idols-*.json.gz";

            _db = services.GetRequiredService<Database>();
            _store = services.GetRequiredService<UpdateStore>();
        }

        protected override async Task ProcessFile(string filename, IAsyncEnumerable<JToken> entries)
        {
            var timestamp = ExtractTimestampFromFilename(filename, @"idols-(\d+)\.json\.gz");
            if (timestamp == null)
                return;

            await using var conn = await _db.Obtain();
            await using var tx = await conn.BeginTransactionAsync();
            
            var updates = await entries
                .Select(entry => EntityUpdate.From(UpdateType.Idols, _sourceId, timestamp.Value, entry))
                .ToListAsync();

            var res = await _store.SaveUpdates(conn, updates);
            if (res > 0)
                _logger.Information("- Imported idols update at {Timestamp}", timestamp);

            await tx.CommitAsync();
        }

        public override async Task Run(ImportOptions options)
        {
            await base.Run(options);
            
            await using var conn = await _db.Obtain();
            await _db.RefreshMaterializedViews(conn, "idols_versions");
        }
    }
}
