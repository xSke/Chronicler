using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json.Linq;
using NodaTime;
using NodaTime.Extensions;
using SIBR.Storage.Data;
using SIBR.Storage.Data.Models;
using SIBR.Storage.Data.Utils;
using SIBR.Storage.Ingest.Utils;

namespace SIBR.Storage.Ingest
{
    public class ElectionResultsWorker: IntervalWorker
    {
        private readonly Guid _sourceId;
        private readonly ElectionResultsConfiguration _config;
        private readonly HttpClient _client;
        private readonly Database _db;
        private readonly UpdateStore _updateStore;
        private readonly IClock _clock;

        private int _lastResultsSeason = -1;
        private Instant _lastResultsTime = Instant.MinValue;

        public ElectionResultsWorker(IServiceProvider services, ElectionResultsConfiguration config, Guid sourceId) : base(services, config)
        {
            _sourceId = sourceId;
            _config = config;
            _db = services.GetRequiredService<Database>();
            _updateStore = services.GetRequiredService<UpdateStore>();
            _client = services.GetRequiredService<HttpClient>();
            _clock = services.GetRequiredService<IClock>();
        }

        protected override async Task RunInterval()
        {
            await using var conn = await _db.Obtain();
            var sim = await _updateStore.GetLatestUpdate(conn, UpdateType.Sim);
            if (sim.Data.Value<int>("phase") != 0)
                // Sim phase 0 is when election results are present
                // this should hopefully stop us from needing to poll it constantly all the time
                return;
            var season = sim.Data.Value<int>("season");

            // If we already have results for this season, don't poll constantly
            var timeSinceLast = _clock.GetCurrentInstant() - _lastResultsTime;
            if (_lastResultsSeason == season &&
                timeSinceLast < _config.ThrottleInterval.ToDuration())
            {
                _logger.Debug("Already pulled within throttle interval ({TimeSinceLast} < {ThrottleInterval}), stopping", timeSinceLast, _config.ThrottleInterval);
                return;
            }

            _logger.Information("Fetching election results for season {Season}", season);
            await foreach (var result in FetchElectionResults(season)) 
                await _updateStore.SaveUpdate(conn, result);

            _logger.Information("Election results for season {Season} successfully saved!", season);
            _lastResultsSeason = season;
            _lastResultsTime = _clock.GetCurrentInstant();
        }

        // Make this an AsyncEnumerable so we can still get partial data saved if the servers break in the middle of a pull
        private async IAsyncEnumerable<EntityUpdate> FetchElectionResults(int season)
        {
            using var hasher = new SibrHasher();
            
            var recap = await _client.GetJsonAsync($"https://www.blaseball.com/database/offseasonRecap?season={season}");
            yield return EntityUpdate.From(UpdateType.OffseasonRecap, _sourceId, recap.Timestamp, recap.Data, hasher);

            var decreeIds = recap.Data.Value<JArray>("decreeResults").Values<string>().ToList();
            var decreeResults = await GetUpdatesByIds(UpdateType.DecreeResult, "https://www.blaseball.com/database/decreeResults", decreeIds, hasher);
            _logger.Information("Fetched {DecreeCount} decree results", decreeIds.Count);
            foreach (var result in decreeResults)
                yield return result;

            var bonusIds = recap.Data.Value<JArray>("bonusResults").Values<string>().ToList();
            var bonusResults = await GetUpdatesByIds(UpdateType.BonusResult, "https://www.blaseball.com/database/bonusResults", bonusIds, hasher);
            _logger.Information("Fetched {BonusCount} bonus results", bonusIds.Count);
            foreach (var result in bonusResults)
                yield return result;

            var eventIds = recap.Data.Value<JArray>("eventResults").Values<string>().ToList();
            var eventResults = await GetUpdatesByIds(UpdateType.EventResult, "https://www.blaseball.com/database/eventResults", eventIds, hasher);
            _logger.Information("Fetched {EventCount} event results", eventIds.Count);
            foreach (var result in eventResults)
                yield return result;
        }

        private async Task<IEnumerable<EntityUpdate>> GetUpdatesByIds(UpdateType type, string baseUrl, IEnumerable<string> ids, SibrHasher hasher)
        {
            var (timestamp, data) = await _client.GetJsonAsync($"{baseUrl}?ids={string.Join(',', ids)}");
            return data.Values<JObject>().Select(obj =>
            {
                var id = TgbUtils.GenerateGuidFromString(obj.Value<string>("id"));
                return EntityUpdate.From(type, _sourceId, timestamp, obj, hasher, id);
            });
        }
    }
}