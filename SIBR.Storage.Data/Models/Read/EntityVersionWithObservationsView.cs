using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using NodaTime;

namespace SIBR.Storage.Data.Models
{
    public class EntityVersionWithObservationsView : IJsonData
    {
        public UpdateType Type { get; set; }
        public Guid EntityId { get; set; }
        public int Version { get; set; }
        public Guid Hash { get; set; }
        public JsonElement Data { get; set; }

        public IEnumerable<Observation> Observations => ObservationTimestamps
            .Zip(ObservationSources)
            .Select(x =>
                new Observation
                {
                    Timestamp = x.First,
                    SourceId = x.Second
                });

        public Instant[] ObservationTimestamps { get; set; }
        public Guid[] ObservationSources { get; set; }
    }
}