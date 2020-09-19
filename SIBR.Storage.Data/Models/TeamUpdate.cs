using System;
using Newtonsoft.Json.Linq;
using SIBR.Storage.Data.Utils;

namespace SIBR.Storage.Data.Models
{
    public class TeamUpdate: StoredObject
    {
        public DateTimeOffset Timestamp { get; set; }
        public Guid TeamId { get; set; }
        
        public TeamUpdate(DateTimeOffset timestamp, JObject data) : base(data)
        {
            Timestamp = timestamp;
            TeamId = TgbUtils.GetId(data);
        }
    }
}