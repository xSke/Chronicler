using System;
using System.Text.Json.Serialization;
using NodaTime;
using SIBR.Storage.API.Utils;
using SIBR.Storage.Data.Models;

namespace SIBR.Storage.API.Models
{
    public class ApiRosterUpdate
    {
        public Guid PlayerId { get; set; }
        public Guid TeamId { get; set; }
        
        [JsonConverter(typeof(LowercaseStringNullableEnumConverter<PlayerView.TeamPosition>))]
        public PlayerView.TeamPosition Position { get; set; }
        public int RosterIndex { get; set; }
        
        public Instant FirstSeen { get; set; }
        public Instant LastSeen { get; set; }

        public ApiRosterUpdate(RosterUpdateView db)
        {
            PlayerId = db.PlayerId;
            TeamId = db.TeamId;
            Position = db.Position;
            RosterIndex = db.RosterIndex;
            FirstSeen = db.FirstSeen;
            LastSeen = db.LastSeen;
        }
    }
}