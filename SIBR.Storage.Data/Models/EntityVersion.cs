using System;
using System.Text.Json;
using NodaTime;
using SIBR.Storage.Data.Query;

namespace SIBR.Storage.Data.Models
{
    public class EntityVersion
    {
        public UpdateType Type { get; set; }
        public Guid EntityId { get; set; }
        public Guid Hash { get; set; }
        public Instant ValidFrom { get; set; }
        public Instant? ValidTo { get; set; }
        public JsonElement Data { get; set; }
        
        public PageToken NextPage => new PageToken(ValidFrom, EntityId);
    }
}