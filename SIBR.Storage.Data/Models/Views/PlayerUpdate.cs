using System;
using System.Text.Json;
using NodaTime;

namespace SIBR.Storage.Data.Models
{
    public class PlayerUpdate
    {
        public Guid UpdateId { get; set; }
        public Instant FirstSeen { get; set; }
        public Instant LastSeen { get; set; }
        public Guid PlayerId { get; set; }
        public Guid Hash { get; set; }
        public JsonElement Data { get; set; }
    }
}