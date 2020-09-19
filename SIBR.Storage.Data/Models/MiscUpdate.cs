using System;
using Newtonsoft.Json.Linq;

namespace SIBR.Storage.Data.Models
{
    public class MiscUpdate: StoredObject
    {
        public const string GlobalEvents = "global_events";
        public const string Idols = "idols";
        public const string OffseasonSetup = "offseason_setup";
        public const string Sim = "sim";
        public const string Temporal = "temporal";
        
        public DateTimeOffset Timestamp;
        public string Type;

        public MiscUpdate(string type, DateTimeOffset timestamp, JToken data) : base(data)
        {
            Timestamp = timestamp;
            Type = type;
        }
    }
}