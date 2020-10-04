using System;
using System.Text.Json;
using NodaTime;

namespace SIBR.Storage.Data.Models
{
    public class EntityUpdateView: IJsonData
    {
        public Guid UpdateId { get; set; }
        public UpdateType Type { get; set; }
        public Guid SourceId { get; set; }
        public Instant Timestamp { get; set; }
        public Guid? EntityId { get; set; }
        public Guid Hash { get; set; }
        public JsonElement Data { get; set; }
    }
}