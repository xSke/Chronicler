using System;
using Newtonsoft.Json.Linq;
using SIBR.Storage.Data.Utils;

namespace SIBR.Storage.Data.Models
{
    public class GameUpdate: StoredObject
    {
        public DateTimeOffset Timestamp;
        public Guid GameId;

        public GameUpdate(DateTimeOffset timestamp, JObject data) : base(data)
        {
            Timestamp = timestamp;
            GameId = TgbUtils.GetId(data);
        }
    }
}