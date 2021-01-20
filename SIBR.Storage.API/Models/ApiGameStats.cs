using System;
using System.Text.Json;
using NodaTime;
using SIBR.Storage.Data.Models;

namespace SIBR.Storage.API.Models
{
    public class ApiGameStats
    {
        public Guid GameId { get; set; }
        public Instant Timestamp { get; set; }
        public JsonElement GameStats { get; set; }
        public JsonElement[] TeamStats { get; set; }
        public JsonElement[] PlayerStats { get; set; }
    }
}
