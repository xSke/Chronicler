using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json.Linq;
using NodaTime;
using SIBR.Storage.Data;
using SIBR.Storage.Data.Models;

namespace SIBR.Storage.Ingest
{
    public class MiscEndpointWorker : IntervalWorker
    {
        private readonly HttpClient _client;
        private readonly Database _db;
        private readonly UpdateStore _updateStore;
        private readonly IClock _clock;
        private readonly MiscEndpointWorkerConfiguration _config;
        private readonly Guid _sourceId;

        public MiscEndpointWorker(IServiceProvider services, MiscEndpointWorkerConfiguration config, Guid sourceId) :
            base(services, config)
        {
            _config = config;
            _sourceId = sourceId;
            _client = services.GetRequiredService<HttpClient>();
            _updateStore = services.GetRequiredService<UpdateStore>();
            _db = services.GetRequiredService<Database>();
            _clock = services.GetRequiredService<IClock>();
        }

        protected override async Task RunInterval()
        {
            var updates = await Task.WhenAll(_config.Endpoints.Select(PollEndpoint));

            await using var conn = await _db.Obtain();
            await using (var tx = await conn.BeginTransactionAsync())
            {
                var res = await _updateStore.SaveUpdates(conn, updates.SelectMany(u => u).ToList());
                _logger.Information("Saved {NewUpdates} misc. updates", res);
                await tx.CommitAsync();
            }

            if (_config.MaterializedViews != null) 
                await _db.RefreshMaterializedViews(conn, _config.MaterializedViews.ToArray());
        }

        private async Task<IEnumerable<EntityUpdate>> PollEndpoint(
            IngestEndpoint endpoint)
        {
            try
            {
                var timestamp = _clock.GetCurrentInstant();
                var json = await _client.GetStringAsync(endpoint.Url);
                var token = JToken.Parse(json);

                var update = EntityUpdate.From(endpoint.Type, _sourceId, timestamp, token);
                _logger.Information("- Fetched endpoint {Endpoint} ({UpdateType}, hash {Hash})", endpoint.Url, endpoint.Type, update.Hash);
                return new[] {update};
            }
            catch (Exception e)
            {
                _logger.Error(e, "Error reading endpoint {Endpoint}", endpoint.Url);
            }

            return ImmutableList<EntityUpdate>.Empty;
        }
    }
}