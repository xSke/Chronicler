using System.Text.Json;
using NodaTime;
using SIBR.Storage.Data.Models.Read;

namespace SIBR.Storage.API.Models
{
    public class ApiPusherEvent
    {
        public Instant Timestamp { get; set; }
        public string Channel { get; set; }
        public string Event { get; set; }
        public JsonElement? Data { get; set; }

        public ApiPusherEvent(PusherEvent db)
        {
            Timestamp = db.Timestamp;
            Channel = db.Channel;
            Event = db.Event;
            Data = db.Data;
        }
    }
}