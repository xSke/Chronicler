using System;
using System.Text.Json;
using NodaTime;
using SIBR.Storage.Data.Models;

namespace SIBR.Storage.API.Models
{
    public class ApiTeam
    {
        public Guid Id { get; set; }
        public Instant LastUpdate { get; set; }
        public JsonElement Data { get; set; }

        public ApiTeam(TeamView db)
        {
            Id = db.TeamId;
            LastUpdate = db.Timestamp;
            Data = db.Data;
        }
    }
}