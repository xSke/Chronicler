using System;
using System.Collections.Generic;
using NodaTime;
using SIBR.Storage.Data.Models;

namespace SIBR.Storage.Ingest
{
    public class IngestWorkers
    {
        public static IEnumerable<BaseWorker> CreateWorkers(IServiceProvider services, Guid sourceId) => new BaseWorker[]
        {
            new MiscEndpointWorker(services, Duration.FromMinutes(1), sourceId, new[]
            {
                (UpdateType.Idols, "https://www.blaseball.com/api/getIdols"),
                (UpdateType.Tributes, "https://www.blaseball.com/api/getTribute"),
                (UpdateType.GlobalEvents, "https://www.blaseball.com/database/globalEvents"),
                (UpdateType.Sim, "https://www.blaseball.com/database/simulationData"),
            }, new []{"idols_versions", "simdata_versions", "globalevents_versions", "temporal_versions"}) { Offset = TimeSpan.FromSeconds(1) },
            new MiscEndpointWorker(services, Duration.FromMinutes(10),  sourceId, new[]
            {
                (UpdateType.OffseasonSetup, "https://www.blaseball.com/database/offseasonSetup"),
            }, new [] { "tributes_versions", "tributes_by_player", "tributes_hourly" }), // these matviews are slow so don't update them as often... 
            new SiteUpdateWorker(services, sourceId),
            new StreamDataWorker(services, sourceId), 
            new TeamPlayerDataWorker(services, sourceId),
            new GameEndpointWorker(services, sourceId),
            new FutureGamesWorker(services, sourceId),
            new StatsheetsWorker(services, sourceId), 
        };
    }
}