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
        
        [JsonIgnore] public int Season => Data.GetProperty("season").GetInt32();
        [JsonIgnore] public int Day => Data.GetProperty("day").GetInt32();
    }
}