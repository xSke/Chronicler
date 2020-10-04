using System;
using System.Text.Json;
using NodaTime;
using SIBR.Storage.Data.Query;

namespace SIBR.Storage.Data.Models
{
    public class GameUpdateView: IGameData, IPaginatedView
    {
        public Guid GameId { get; set; }
        public Instant Timestamp { get; set; }
        public Guid Hash { get; set; }
        public JsonElement Data { get; set; }
        
        public PageToken NextPage => new PageToken(Timestamp, Hash);
    }
}