using System;
using Newtonsoft.Json.Linq;

namespace SIBR.Storage.Data.Models
{
    public class StreamUpdate: StoredObject
    {
        public DateTimeOffset Timestamp;
        
        public StreamUpdate(DateTimeOffset timestamp, JToken data) : base(data)
        {
            Timestamp = timestamp;
        }
    }
}