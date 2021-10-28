using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NodaTime;
using SIBR.Storage.Data.Utils;

namespace SIBR.Storage.Data.Models
{
    public class GameUpdate: IJsonObject
    {
        [JsonIgnore] public Guid SourceId { get; set; }
        public Instant Timestamp { get; set; }
        public Guid Hash { get; set; }
        public JToken Data { get; set; }
        public Guid GameId { get; set; }
        [JsonIgnore] public int Season { get; set; }
        [JsonIgnore] public int Day { get; set; }
        [JsonIgnore] public int Tournament { get; set; }
        [JsonIgnore] public int PlayCount { get; set; }
        [JsonIgnore] public string Sim { get; set; }

        public static GameUpdate From(Guid sourceId, Instant timestamp, JToken data, SibrHasher hasher = null) =>
            new GameUpdate
            {
                SourceId = sourceId,
                Timestamp = timestamp,
                Hash = hasher?.HashToken(data) ?? SibrHash.HashAsGuid(data),
                Data = data,
                GameId = TgbUtils.TryGetId(data) ?? throw new ArgumentException("Game did not have id"),
                Season = data.Value<int>("season"),
                Day = data.Value<int>("day"),
                Tournament = data.Value<int?>("tournament") ?? -1,
                PlayCount = data.Value<int?>("playCount") ?? -1,
                Sim = data.Value<string?>("sim") ?? "thisidisstaticyo"
            };

        public static IEnumerable<GameUpdate> FromArray(Guid sourceId, Instant timestamp,
            IEnumerable<JToken> data, SibrHasher hasher = null) =>
            data.Select(item => From(sourceId, timestamp, item, hasher));
    }
}