using System;
using Newtonsoft.Json.Linq;

namespace SIBR.Storage.Data.Models
{
    public class GameUpdate
    {
        public DateTimeOffset Timestamp;
        public Guid Hash;
        public Guid GameId;
        public JObject Data;
    }
}