using System;
using System.Collections.Generic;
using SIBR.Storage.Data.Models;

namespace SIBR.Storage.Ingest
{
    public class IngestConfiguration
    {
        public Guid SourceId { get; set; }
        public int StreamCount { get; set; }
        public IntervalWorkerConfiguration FutureGamesWorker { get; set; }
        public IntervalWorkerConfiguration FeedWorker { get; set; }
        public IntervalWorkerConfiguration GameEndpointWorker { get; set; }
        public List<MiscEndpointWorkerConfiguration> MiscEndpointWorkers { get; set; }
        public IntervalWorkerConfiguration SiteUpdateWorker { get; set; }
        public IntervalWorkerConfiguration StatsheetsWorker { get; set; }
        public IntervalWorkerConfiguration TeamPlayerWorker { get; set; }
        public IntervalWorkerConfiguration TeamElectionWorker { get; set; }
        public IntervalWorkerConfiguration LibraryStoryWorker { get; set; }
        public IntervalWorkerConfiguration AvailableBetsWorker { get; set; }
        public IntervalWorkerConfiguration GammaElectionsWorker { get; set; }
        public ThrottledIntervalWorkerConfiguration RenovationsWorker { get; set; }
        public ThrottledIntervalWorkerConfiguration ElectionResultsWorker { get; set; }
    }

    public class IntervalWorkerConfiguration
    {
        public TimeSpan Interval { get; set; }
        public TimeSpan Offset { get; set; }
    }

    public class ThrottledIntervalWorkerConfiguration: IntervalWorkerConfiguration
    {
        public TimeSpan ThrottleInterval { get; set; }
    }
    
    public class MiscEndpointWorkerConfiguration: IntervalWorkerConfiguration
    {
        public List<IngestEndpoint> Endpoints { get; set; }
        public List<string> MaterializedViews { get; set; }
    }

    public class IngestEndpoint
    {
        public string Url { get; set; }
        public UpdateType Type { get; set; }
    }
}