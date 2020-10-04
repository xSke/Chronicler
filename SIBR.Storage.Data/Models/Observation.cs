using System;
using NodaTime;

namespace SIBR.Storage.Data.Models
{
    public struct Observation
    {
        public Instant Timestamp { get; set; }
        public Guid SourceId { get; set; }
    }
}