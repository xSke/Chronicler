using System;
using System.Text.Json;
using NodaTime;

namespace SIBR.Storage.Data.Models
{
    public class EntityVersion
    {
        public UpdateType Type;
        public Guid EntityId;
        public int Version;
        public Guid Hash;
        public JsonElement Data;
        public Instant[] Observations;
        public Guid[] ObservationSources;
    }
}