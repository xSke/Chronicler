using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using NodaTime;
using SIBR.Storage.API.Utils;
using SIBR.Storage.Data.Models;

namespace SIBR.Storage.API.Controllers.Models
{
    public class ApiPlayer
    {
        public Guid Id { get; set; }
        public Guid TeamId { get; set; }
        public Instant LastUpdate { get; set; }

        [JsonConverter(typeof(LowercaseNamingPolicy))]
        public Player.TeamPosition Position { get; set; }

        public int RosterIndex { get; set; }
        
        public JsonElement Data { get; set; }
    }
}