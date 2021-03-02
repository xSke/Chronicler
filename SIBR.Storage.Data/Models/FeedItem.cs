using System;
using Newtonsoft.Json.Linq;
using NodaTime;

namespace SIBR.Storage.Data.Models
{
    public class FeedItem
    {
        public Guid Id { get; set; }
        public Instant Timestamp { get; set; }
        public JObject Data { get; set; }
    }
}