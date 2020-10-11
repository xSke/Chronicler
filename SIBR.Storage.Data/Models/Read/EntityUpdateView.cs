using System;
using System.Text.Json;
using NodaTime;
using SIBR.Storage.Data.Query;

namespace SIBR.Storage.Data.Models
{
    public class EntityUpdateView: IJsonData, IPaginatedView
    {
        public Guid UpdateId { get; set; }
        public UpdateType Type { get; set; }
        public Guid SourceId { get; set; }
        public Instant Timestamp { get; set; }
        public Guid? EntityId { get; set; }
        public Guid Hash { get; set; }
        public JsonElement Data { get; set; }
        
        public PageToken NextPage => new PageToken(Timestamp, UpdateId);
    }
}