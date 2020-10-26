using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using NodaTime;
using SIBR.Storage.Data.Utils;

namespace SIBR.Storage.Data.Models
{
    public class EntityUpdate : BareEntityUpdate, IJsonObject
    {
        public JToken Data { get; set; }

        public static EntityUpdate From(UpdateType type, Guid sourceId, Instant timestamp, JToken data, SibrHasher hasher = null, Guid? idOverride = null) =>
            new EntityUpdate
            {
                Type = type,
                SourceId = sourceId,
                EntityId = idOverride ?? TgbUtils.TryGetId(data) ?? default,
                Timestamp = timestamp,
                Hash = hasher?.HashToken(data) ?? SibrHash.HashAsGuid(data),
                Data = data,
            };

        public static IEnumerable<EntityUpdate> FromArray(UpdateType type, Guid sourceId, Instant timestamp,
            IEnumerable<JToken> data, SibrHasher hasher = null) =>
            data.Select(item => From(type, sourceId, timestamp, item, hasher));
    }
}