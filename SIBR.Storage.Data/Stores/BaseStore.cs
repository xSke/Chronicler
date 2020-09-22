using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Npgsql;
using Serilog;
using SIBR.Storage.Data.Models;

namespace SIBR.Storage.Data
{
    public abstract class BaseStore
    {
        protected readonly ILogger _logger;

        protected BaseStore(IServiceProvider services)
        {
            _logger = services.GetRequiredService<ILogger>().ForContext(GetType());
        }

        public async Task RefreshMaterializedViews(NpgsqlConnection conn, params string[] matViews)
        {
            foreach (var matView in matViews)
            {
                var sw = new Stopwatch();
                sw.Start();
                await conn.ExecuteAsync($"refresh materialized view concurrently {matView}");
                sw.Stop();
                
                _logger.Information("Refreshed materialized view {ViewName} in {Duration}", matView, sw.Elapsed);
            }
        }
        
        protected static async Task SaveObjects(NpgsqlConnection conn, IEnumerable<IJsonHashedObject> updates)
        {
            var dictionary = new Dictionary<Guid, JToken>();
            foreach (var obj in updates)
                dictionary[obj.Hash] = obj.Data;
            var entries = dictionary.ToList();

            await conn.ExecuteAsync(
                "insert into objects (hash, data) select unnest(@Hash), unnest(@Data)::jsonb on conflict do nothing",
                new
                {
                    Hash = entries.Select(e => e.Key).ToArray(),
                    Data = entries.Select(e => e.Value.ToString(Formatting.None)).ToArray()
                });
        }
    }
}