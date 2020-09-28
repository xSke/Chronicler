using System;
using System.Text.Json;
using NodaTime;

namespace SIBR.Storage.Data.Models
{
    public class EntityUpdateView
    {
        public UpdateType Type;
        public Guid SourceId;
        public Instant Timestamp;
        public Guid? EntityId;
        public Guid Hash;
        public JsonElement Data;
    }
}