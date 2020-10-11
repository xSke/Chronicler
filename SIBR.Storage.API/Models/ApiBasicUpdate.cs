using System;
using System.Text.Json;
using NodaTime;
using SIBR.Storage.Data.Models;

namespace SIBR.Storage.API.Models
{
    public class ApiBasicUpdate
    {
        public Guid UpdateId { get; set; }
        public Guid Hash { get; set; }
        public Instant Timestamp { get; set; }
        public JsonElement Data { get; set; }
        
        public ApiBasicUpdate(EntityUpdateView db)
        {
            UpdateId = db.UpdateId;
            Hash = db.Hash;
            Timestamp = db.Timestamp;
            Data = db.Data;
        }
    }
}