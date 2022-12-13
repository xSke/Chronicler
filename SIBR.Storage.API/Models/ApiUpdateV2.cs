using System;
using System.Text.Json;
using NodaTime;
using SIBR.Storage.Data.Models;

namespace SIBR.Storage.API.Models
{
    public class ApiUpdateV2
    {
        public string Type { get; set; }
        public Guid EntityId { get; set; }
        public Guid Hash { get; set; }
        public Instant Timestamp { get; set; }
        public JsonElement Data { get; set; }


        public ApiUpdateV2(EntityUpdateResponse update)
        {
            Type = update.Type.ToString();
            EntityId = update.EntityId;
            Hash = update.Hash;
            Timestamp = update.Timestamp;
            Data = update.Data;
        }
    }
}