using System;
using System.Text.Json;
using NodaTime;

namespace SIBR.Storage.Data.Models
{
    public class GameView: IGameData
    {
        public Guid GameId { get; set; }
        public Guid Statsheet { get; set; }
        public Instant? StartTime { get; set; }
        public Instant? EndTime { get; set; }
        public JsonElement Data { get; set; }
    }
}