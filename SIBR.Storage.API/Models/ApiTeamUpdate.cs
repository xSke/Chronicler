using System;
using System.Text.Json;
using NodaTime;
using SIBR.Storage.Data.Models;
using SIBR.Storage.Data.Query;

namespace SIBR.Storage.API.Models
{
    public class ApiTeamUpdate
    {
        public Guid UpdateId { get; set; }
        public Guid TeamId { get; set; }
        public Instant FirstSeen { get; set; }
        public Instant LastSeen { get; set; }
        public JsonElement Data { get; set; }

        public ApiTeamUpdate(TeamUpdateView db)
        { 
            UpdateId = db.UpdateId;
            TeamId = db.TeamId;
            FirstSeen = db.FirstSeen;
            LastSeen = db.LastSeen;
            Data = db.Data;
        }
    }
}