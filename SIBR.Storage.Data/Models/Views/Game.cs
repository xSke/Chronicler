using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using NodaTime;

namespace SIBR.Storage.Data.Models
{
    public class Game
    {
        [JsonPropertyName("id")] public Guid GameId { get; set; }
        [JsonPropertyName("start")] public Instant? StartTime { get; set; }
        [JsonPropertyName("end")] public Instant? EndTime { get; set; }
        public JsonElement Data { get; set; }
    }
}