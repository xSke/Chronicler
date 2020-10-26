using System;
using NodaTime;

namespace SIBR.Storage.Data.Models
{
    public class EntityObservationView
    {
        public int Type { get; set; }
        public Instant Timestamp { get; set; }
        public Guid Hash { get; set; }
        public Guid SourceId { get; set; }
        public Guid EntityId { get; set; }
        public Guid UpdateId { get; set; }
    }
}