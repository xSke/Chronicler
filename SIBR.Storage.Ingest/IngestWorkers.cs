using System;
using System.Collections.Generic;
using Microsoft.Extensions.DependencyInjection;
using SIBR.Storage.Data.Models;

namespace SIBR.Storage.Ingest
{
    public class IngestWorkers
    {
        public static IEnumerable<BaseWorker> AllWorkers(IServiceProvider services) => new BaseWorker[]
        {
            new MiscEndpointWorker(services, new[]
            {
                (MiscUpdate.Idols, "https://www.blaseball.com/api/getIdols"),
                (MiscUpdate.Tributes, "https://www.blaseball.com/api/getTribute"),
                (MiscUpdate.GlobalEvents, "https://www.blaseball.com/database/globalEvents"),
                (MiscUpdate.OffseasonSetup, "https://www.blaseball.com/database/offseasonSetup"),
                (MiscUpdate.Sim, "https://www.blaseball.com/database/simulationData"),
            }), 
            services.GetRequiredService<SiteUpdateWorker>(),
            services.GetRequiredService<StreamDataWorker>(),
            services.GetRequiredService<TeamPlayerDataWorker>()
        };
    }
}