using System;
using System.Text.Json;
using NodaTime;
using SIBR.Storage.Data.Models;

namespace SIBR.Storage.API.Models
{
    public class ApiVersionV2
    {
        public Guid EntityId { get; set; }
        public Guid Hash { get; set; }
        public Instant ValidFrom { get; set; }
        public Instant? ValidTo { get; set; }
        public JsonElement Data { get; set; }

        public ApiVersionV2(EntityVersion version)
        {
            EntityId = version.EntityId;
            Hash = version.Hash;
            ValidFrom = version.ValidFrom;
            ValidTo = version.ValidTo;
            Data = version.Data;
        }
    }
}