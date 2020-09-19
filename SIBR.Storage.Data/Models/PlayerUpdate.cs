using System;
using Newtonsoft.Json.Linq;
using SIBR.Storage.Data.Utils;

namespace SIBR.Storage.Data.Models
{
    public class PlayerUpdate: StoredObject
    {
        public DateTimeOffset Timestamp { get; set; }
        public Guid PlayerId { get; set; }
        
        public PlayerUpdate(DateTimeOffset timestamp, JObject data) : base(data)
        {
            Timestamp = timestamp;
            PlayerId = TgbUtils.GetId(data);
        }
    }
}