using System;
using System.Text.Json;
using NodaTime;
using SIBR.Storage.Data.Models;

namespace SIBR.Storage.API.Models
{
    public class ApiFightUpdate
    {
        public Guid FightId { get; set; }
        public Instant Timestamp { get; set; }
        public Guid Hash { get; set; }
        public JsonElement Data { get; set; }

        public ApiFightUpdate(EntityUpdateView db)
        {
            FightId = db.EntityId ?? default;
            Timestamp = db.Timestamp;
            Hash = db.Hash;
            Data = db.Data;
        }
    }
}