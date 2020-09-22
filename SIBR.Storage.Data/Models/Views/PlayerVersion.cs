using System;
using Newtonsoft.Json.Linq;
using NodaTime;

namespace SIBR.Storage.Data.Models
{
    public class PlayerVersion
    {
        public Instant FirstSeen { get; set; }
        public Instant LastSeen { get; set; }
        public Guid PlayerId { get; set; }
        public Guid Hash { get; set; }
        public JObject Data { get; set; }
    }
}