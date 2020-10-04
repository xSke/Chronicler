using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using NodaTime;
using SIBR.Storage.API.Utils;
using SIBR.Storage.Data.Models;
using SIBR.Storage.Data.Utils;

namespace SIBR.Storage.API.Models
{
    public class ApiPlayer
    {
        public Guid Id { get; set; }
        public Guid? TeamId { get; set; }
        public Instant LastUpdate { get; set; }

        [JsonConverter(typeof(LowercaseStringEnumConverter))]
        public PlayerView.TeamPosition? Position { get; set; }

        public int? RosterIndex { get; set; }
        
        public JsonElement Data { get; set; }
        public PlayerStars Stars { get; }
        
        public ApiPlayer(PlayerView db)
        {
            Id = db.PlayerId;
            TeamId = db.TeamId;
            LastUpdate = db.Timestamp;
            Position = db.Position;
            RosterIndex = db.RosterIndex;
            Data = db.Data;
            Stars = ((IPlayerData) db).Stars;
        }
    }
}