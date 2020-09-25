using System;
using System.Text.Json;
using NodaTime;

namespace SIBR.Storage.Data.Models
{
    public class GameUpdateView
    {
        public Guid GameId { get; set; }
        public Instant Timestamp { get; set; }
        public Guid Hash { get; set; }
        public JsonElement Data { get; set; }
    }
}