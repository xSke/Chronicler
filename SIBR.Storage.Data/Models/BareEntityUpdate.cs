using System;
using NodaTime;

namespace SIBR.Storage.Data.Models
{
    public class BareEntityUpdate
    {
        public UpdateType Type { get; set; }
        public Guid SourceId { get; set; }
        public Instant Timestamp { get; set; }
        public Guid? EntityId { get; set; }
        public Guid Hash { get; set; }
    }
}