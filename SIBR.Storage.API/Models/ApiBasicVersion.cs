using System;
using System.Text.Json;
using NodaTime;
using SIBR.Storage.Data.Models;

namespace SIBR.Storage.API.Models
{
    public class ApiBasicVersion
    {
        public Guid UpdateId { get; set; }
        public Instant FirstSeen { get; set; }
        public Instant LastSeen { get; set; }
        public JsonElement Data { get; set; }
        
        public ApiBasicVersion(EntityVersionView db)
        {
            UpdateId = db.UpdateId;
            FirstSeen = db.FirstSeen;
            LastSeen = db.LastSeen;
            Data = db.Data;
        }
    }
}