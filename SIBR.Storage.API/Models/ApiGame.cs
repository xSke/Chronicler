using System;
using System.Text.Json;
using NodaTime;
using SIBR.Storage.Data.Models;

namespace SIBR.Storage.API.Models
{
    public class ApiGame
    {
        public Guid GameId { get; set; }
        public Instant? StartTime { get; set; }
        public Instant? EndTime { get; set; }
        public JsonElement Data { get; set; }

        public ApiGame(GameView db)
        {
            GameId = db.GameId;
            StartTime = db.StartTime;
            EndTime = db.EndTime;
            Data = db.Data;
        }
    }
}