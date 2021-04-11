using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json.Linq;
using NodaTime;
using NodaTime.Extensions;
using Npgsql;
using SIBR.Storage.Data;
using SIBR.Storage.Data.Models;

namespace SIBR.Storage.Ingest
{
    public class RenovationsWorker: IntervalWorker
    {
        private readonly ThrottledIntervalWorkerConfiguration _config;
        private readonly Guid _sourceId;
        private readonly HttpClient _client;
        private readonly UpdateStore _updateStore;
        private readonly Database _db;
        private readonly IClock _clock;
        private Instant? _lastPollTime;
        
        public RenovationsWorker(IServiceProvider services, ThrottledIntervalWorkerConfiguration config, Guid sourceId) : base(services, config)
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
            await using var conn = await _db.Obtain();

            if (!await ShouldPollFast(conn))
            {
                // If we already have results for this season, don't poll constantly
                var timeSinceLast = _clock.GetCurrentInstant() - _lastPollTime;
                if (timeSinceLast < _config.ThrottleInterval.ToDuration())
                {
                    _logger.Debug("Already pulled within throttle interval ({TimeSinceLast} < {ThrottleInterval}), stopping", timeSinceLast, _config.ThrottleInterval);
                    return;
                }
            }

            var stadiums = await _updateStore.GetLatestUpdatesFor(UpdateType.Stadium).ToListAsync();
            var renovations = await QueryAllRenovations(stadiums.Select(s => s.EntityId!.Value));
            _logger.Information("Fetched {RenovationCount} renovation progress items", renovations.Length);
            await _updateStore.SaveUpdates(conn, renovations);

            _lastPollTime = _clock.GetCurrentInstant();
        }

        private async Task<EntityUpdate[]> QueryAllRenovations(IEnumerable<Guid> stadiumIds)
        {
            var semaphore = new SemaphoreSlim(5);
            return await Task.WhenAll(stadiumIds.Select(async stadiumId =>
            {
                await semaphore.WaitAsync();

                try
                {
                    return await QueryRenovationProgress(stadiumId);
                }
                finally
                {
                    semaphore.Release();
                }
            }));
        }

        private async Task<EntityUpdate> QueryRenovationProgress(Guid stadiumId)
        {
            var data = await _client.GetStringAsync($"https://www.blaseball.com/database/renovationProgress?id={stadiumId}");
            var json = JToken.Parse(data);
            var timestamp = _clock.GetCurrentInstant();
            return EntityUpdate.From(UpdateType.RenovationProgress, _sourceId, timestamp, json, idOverride: stadiumId);
        }

        private async Task<bool> ShouldPollFast(NpgsqlConnection conn)
        {
            var simData = await _updateStore.GetLatestUpdate(conn, UpdateType.Sim);
            var day = simData.Data["day"]!.ToObject<int>();
            var phase = simData.Data["phase"]!.ToObject<int>();

            // Only poll on day 27 and after
            if (day < 26)
                return false;

            // Only poll before Lateseason
            if (phase <= 1 || phase >= 6)
                return false;
            
            // TODO: check to see if they're all on target somehow
            return true;
        }
    }
}