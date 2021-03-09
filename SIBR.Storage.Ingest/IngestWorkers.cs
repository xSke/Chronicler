using System;
using System.Collections.Generic;
using System.Linq;

namespace SIBR.Storage.Ingest
{
    public class IngestWorkers
    {
        public static IEnumerable<BaseWorker> CreateWorkers(IServiceProvider services, IngestConfiguration config)
        {
            var workers = new List<BaseWorker>
            {
                new SiteUpdateWorker(services, config.SiteUpdateWorker, config.SourceId),
                new TeamPlayerDataWorker(services, config.TeamPlayerWorker, config.SourceId),
                new GameEndpointWorker(services, config.GameEndpointWorker, config.SourceId),
                new FutureGamesWorker(services, config.FutureGamesWorker, config.SourceId),
                new StatsheetsWorker(services, config.StatsheetsWorker, config.SourceId),
                new ElectionResultsWorker(services, config.ElectionResultsWorker, config.SourceId),
                new StreamDataWorker(services, config.StreamCount, config.SourceId),
                new RenovationsWorker(services, config.RenovationsWorker, config.SourceId),
                new FeedWorker(services, config.FeedWorker)
            };
            workers.AddRange(config.MiscEndpointWorkers.Select(workerConfig => new MiscEndpointWorker(services, workerConfig, config.SourceId)));
            return workers;
        }
    }
}