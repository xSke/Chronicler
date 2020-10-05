using System;
using System.Text.Json;
using NodaTime;
using SIBR.Storage.Data.Models;

namespace SIBR.Storage.API.Models
{
    public class ApiGameUpdate
    {
        public Guid GameId { get; set; }
        public Instant Timestamp { get; set; }
        public Guid Hash;
        public JsonElement Data { get; set; }

        public ApiGameUpdate(GameUpdateView db)
        {
            GameId = db.GameId;
            Timestamp = db.Timestamp;
            Hash = db.Hash;
            Data = db.Data;
        }
    }
}