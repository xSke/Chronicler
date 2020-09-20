using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json.Linq;
using SIBR.Storage.Data;
using SIBR.Storage.Data.Models;

namespace SIBR.Storage.Ingest
{
    public class MiscEndpointWorker : IntervalWorker
    {
        private readonly (string updateType, string endpoint)[] _endpoints;
        private readonly HttpClient _client;
        private readonly Database _db;
        private readonly MiscStore _miscStore;
        
        public MiscEndpointWorker(IServiceProvider services, (string updateType, string endpoint)[] endpoints) :
            base(services)
        {
            _endpoints = endpoints;
            _client = services.GetRequiredService<HttpClient>();
            _miscStore = services.GetRequiredService<MiscStore>();
            _db = services.GetRequiredService<Database>();
            Interval = TimeSpan.FromMinutes(1);
        }

        protected override async Task RunInterval()
        {
            var updates = await Task.WhenAll(_endpoints.Select(PollEndpoint));

            await using var conn = await _db.Obtain();
            await using var tx = await conn.BeginTransactionAsync();
            var res = await _miscStore.SaveMiscUpdates(conn, updates.SelectMany(u => u).ToList());
            _logger.Information("Saved {NewUpdates} misc. updates ({NewObjects} new)", res.NewUpdates, res.NewObjects);
            await tx.CommitAsync();
        }

        private async Task<IEnumerable<MiscUpdate>> PollEndpoint((string updateType, string endpoint) entry)
        {
            var (type, endpoint) = entry;

            try
            {
                var timestamp = DateTimeOffset.UtcNow;
                var json = await _client.GetStringAsync(endpoint);
                var token = JToken.Parse(json);
                
                var update = new MiscUpdate(type, timestamp, token);
                _logger.Information("- Fetched endpoint {Endpoint} (hash {Hash})", endpoint, update.Hash);
                return new[] {update};
            }
            catch (Exception e)
            {
                _logger.Error(e, "Error reading endpoint {Endpoint}", endpoint);
            }

            return new MiscUpdate[0];
        }
    }
}