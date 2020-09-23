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
        private readonly (UpdateType updateType, string endpoint)[] _endpoints;
        private readonly string[] _refreshViews;
        private readonly HttpClient _client;
        private readonly Database _db;
        private readonly UpdateStore _updateStore;
        private readonly IClock _clock;
        private readonly Guid _sourceId;

        public MiscEndpointWorker(IServiceProvider services, Duration interval, Guid sourceId, (UpdateType updateType, string endpoint)[] endpoints, string[] refreshViews = null) :
            base(services)
        {
            _endpoints = endpoints;
            _refreshViews = refreshViews;
            _sourceId = sourceId;
            _client = services.GetRequiredService<HttpClient>();
            _updateStore = services.GetRequiredService<UpdateStore>();
            _db = services.GetRequiredService<Database>();
            _clock = services.GetRequiredService<IClock>();
            Interval = interval.ToTimeSpan();
        }

        protected override async Task RunInterval()
        {
            var updates = await Task.WhenAll(_endpoints.Select(PollEndpoint));

            await using var conn = await _db.Obtain();
            await using (var tx = await conn.BeginTransactionAsync())
            {
                var res = await _updateStore.SaveUpdates(conn, updates.SelectMany(u => u).ToList());
                _logger.Information("Saved {NewUpdates} misc. updates", res);
                await tx.CommitAsync();
            }

            if (_refreshViews != null) 
                await _db.RefreshMaterializedViews(conn, _refreshViews);
        }

        private async Task<IEnumerable<EntityUpdate>> PollEndpoint(
            (UpdateType updateType, string endpoint) entry)
        {
            var (type, endpoint) = entry;

            try
            {
                var timestamp = _clock.GetCurrentInstant();
                var json = await _client.GetStringAsync(endpoint);
                var token = JToken.Parse(json);

                var update = EntityUpdate.From(type, _sourceId, timestamp, token);
                _logger.Information("- Fetched endpoint {Endpoint} (hash {Hash})", endpoint, update.Hash);
                return new[] {update};
            }
            catch (Exception e)
            {
                _logger.Error(e, "Error reading endpoint {Endpoint}", endpoint);
            }

            return ImmutableList<EntityUpdate>.Empty;
        }
    }
}