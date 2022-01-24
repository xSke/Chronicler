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
            var (timestamp, data) = await _client.GetJsonAsync("https://api.blaseball.com/api/elections");
            await using (var conn = await _db.Obtain())
            {
                var elections = EntityUpdate.From(UpdateType.GammaElections, _sourceId, timestamp, data);
                await _updateStore.SaveUpdate(conn, elections);

                var electionsIndividual = EntityUpdate.FromArray(UpdateType.GammaElection, _sourceId, timestamp, data);
                await _updateStore.SaveUpdates(conn, electionsIndividual.ToList());
                _logger.Information("Saved gamma elections");
            }

            var electionDetails = new List<EntityUpdate>();
            foreach (var election in data)
            {
                var electionId = election["id"]!.ToObject<Guid>();
                var (electionTimestamp, electionData) =
                    await _client.GetJsonAsync($"https://api.blaseball.com/api/elections/{electionId}");

                var update = EntityUpdate.From(UpdateType.GammaElectionDetails, _sourceId, electionTimestamp,
                    electionData, idOverride: electionId);
                electionDetails.Add(update);
            }
            
            await using (var conn = await _db.Obtain())
            {
                await _updateStore.SaveUpdates(conn, electionDetails);
                _logger.Information("Saved {ElectionCount} gamma election details", electionDetails.Count);
            }
        }
    }
}