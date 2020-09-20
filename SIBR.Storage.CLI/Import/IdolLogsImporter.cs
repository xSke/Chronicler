using System;
using System.Collections.Generic;
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
        private readonly MiscStore _miscStore;
        
        public IdolLogsImporter(IServiceProvider services) : base(services)
        {
            FileFilter = "idols-*.json.gz";

            _db = services.GetRequiredService<Database>();
            _miscStore = services.GetRequiredService<MiscStore>();
        }

        protected override async Task ProcessFile(string filename, IAsyncEnumerable<JToken> entries)
        {
            var timestamp = ExtractTimestampFromFilename(filename, @"idols-(\d+)\.json\.gz");
            if (timestamp == null)
                return;

            await using var conn = await _db.Obtain();
            await using var tx = await conn.BeginTransactionAsync();
            
            await foreach (var entry in entries)
            {
                var update = new MiscUpdate(MiscUpdate.Idols, timestamp.Value, entry);
                var res = await _miscStore.SaveMiscUpdates(conn, new[] { update });
                if (res.NewUpdates > 0)
                    _logger.Information("- Imported idols update at {Timestamp}", timestamp);
            }

            await tx.CommitAsync();
        }
    }
}