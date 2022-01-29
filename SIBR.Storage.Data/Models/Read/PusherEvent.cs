using System;
using System.Text.Json;
using NodaTime;

namespace SIBR.Storage.Data.Models.Read
{
    public class PusherEvent
    {
        public Guid Id { get; }
        public string Channel { get; }
        public string Event { get; }
        public Instant Timestamp { get; }
        public string Raw { get; }
        public JsonElement? Data { get; }
    }
}