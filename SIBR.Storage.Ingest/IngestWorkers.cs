﻿using System;
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
                // new StreamDataWorker(services, config.StreamCount, config.SourceId), (crab emoji)
                new RenovationsWorker(services, config.RenovationsWorker, config.SourceId),
                new TeamElectionWorker(services, config.TeamElectionWorker, config.SourceId),
                new LibraryStoryWorker(services, config.LibraryStoryWorker, config.SourceId),
                new AvailableBetsWorker(services, config.AvailableBetsWorker, config.SourceId),
                new FeedWorker(services, config.FeedWorker),
                new GammaElectionsWorker(services, config.GammaElectionsWorker, config.SourceId),
                new PusherWorker(services, config.SourceId)
            };
            workers.AddRange(config.MiscEndpointWorkers.Select(workerConfig => new MiscEndpointWorker(services, workerConfig, config.SourceId)));
            return workers;
        }
    }
}