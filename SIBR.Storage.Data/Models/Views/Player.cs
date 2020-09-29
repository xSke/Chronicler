using System;
using System.Text.Json;
using NodaTime;

namespace SIBR.Storage.Data.Models
{
    public class Player
    {
        public Guid UpdateId { get; set; }
        public Guid PlayerId { get; set; }
        public Instant Timestamp { get; set; }
        public JsonElement Data { get; set; }
        public Guid TeamId { get; set; }
        public TeamPosition Position { get; set; }
        public int RosterIndex { get; set; }

        public enum TeamPosition
        {
            Lineup,
            Rotation,
            Bullpen,
            Bench
        }
    }
}