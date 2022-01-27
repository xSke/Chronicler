using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json.Linq;
using NodaTime;
using SIBR.Storage.Data;
using SIBR.Storage.Data.Models;
using SIBR.Storage.Ingest.Utils;

namespace SIBR.Storage.Ingest
{
    public class GammaElectionsWorker : IntervalWorker
    {
        private readonly HttpClient _client;
        private readonly Database _db;
        private readonly UpdateStore _updateStore;
        private readonly IClock _clock;
        private readonly Guid _sourceId;

        public GammaElectionsWorker(IServiceProvider services, IntervalWorkerConfiguration config, Guid sourceId) :
            base(services, config)
        {
            _sourceId = sourceId;
            _client = services.GetRequiredService<HttpClient>();
            _updateStore = services.GetRequiredService<UpdateStore>();
            _db = services.GetRequiredService<Database>();
            _clock = services.GetRequiredService<IClock>();
        }

        protected override async Task RunInterval()
        {
            var info = await FetchElectionInfo();
            var splitInfo = SplitElectionInfo(info);
            _logger.Information("Fetched {ElectionCount} gamma elections", splitInfo.Count);

            var detailsTasks = splitInfo
                .Select(u => u.Data["id"]!.ToObject<Guid>())
                .Select(FetchElectionDetails);
            var details = await Task.WhenAll(detailsTasks);
            _logger.Information("Fetched {DetailsCount} gamma election details", details.Length);

            var resultsTasks = details
                .SelectMany(u => u.Data as JArray)
                .Where(u => u["executed"]!.ToObject<bool>())
                .Select(u => u["id"]!.ToObject<Guid>())
                .Select(FetchElectionResults);
            var results = await Task.WhenAll(resultsTasks);
            _logger.Information("Fetched {ResultsCount} gamma election results", results.Length);

            await using var conn = await _db.Obtain();
            await _updateStore.SaveUpdate(conn, info);
            await _updateStore.SaveUpdates(conn, splitInfo);
            await _updateStore.SaveUpdates(conn, details);
            await _updateStore.SaveUpdates(conn, results);
            _logger.Information("Saved gamma election data");
        }
        
        private async Task<EntityUpdate> FetchElectionInfo()
        {
            var (timestamp, data) = await _client.GetJsonAsync("https://api.blaseball.com/api/elections");
            return EntityUpdate.From(UpdateType.GammaElections, _sourceId, timestamp, data);
        }
        
        private List<EntityUpdate> SplitElectionInfo(EntityUpdate root) => 
            EntityUpdate.FromArray(UpdateType.GammaElection, _sourceId, root.Timestamp, root.Data).ToList();

        private async Task<EntityUpdate> FetchElectionDetails(Guid electionId)
        {
            var (electionTimestamp, electionData) =
                await _client.GetJsonAsync($"https://api.blaseball.com/api/elections/{electionId}");

            return EntityUpdate.From(UpdateType.GammaElectionDetails, _sourceId, electionTimestamp,
                electionData, idOverride: electionId);
        }
        
        private async Task<EntityUpdate> FetchElectionResults(Guid prizeId)
        {
            var (timestamp, data) =
                await _client.GetJsonAsync($"https://api.blaseball.com/api/elections/{prizeId}/results");

            return EntityUpdate.From(UpdateType.GammaElectionResults, _sourceId, timestamp,
                data, idOverride: prizeId);
        }
    }
}