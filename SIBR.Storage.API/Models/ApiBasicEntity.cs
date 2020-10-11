using System;
using System.Text.Json;
using NodaTime;
using SIBR.Storage.Data.Models;

namespace SIBR.Storage.API.Models
{
    public class ApiBasicEntity
    {
        public Guid? Id { get; set; }
        public Instant LastUpdate { get; set; }
        public JsonElement Data { get; set; }

        public ApiBasicEntity(EntityUpdateView db)
        {
            Id = db.EntityId;
            LastUpdate = db.Timestamp;
            Data = db.Data;
        }
    }
}