using System;
using System.Text.Json;
using NodaTime;
using SIBR.Storage.Data;
using SIBR.Storage.Data.Models;
using SIBR.Storage.Data.Query;
using SIBR.Storage.Data.Utils;

namespace SIBR.Storage.API.Models
{
    public class ApiPlayerUpdate
    {
        public Guid UpdateId { get; set; }
        public Guid PlayerId { get; set; }
        public Instant FirstSeen { get; set; }
        public Instant LastSeen { get; set; }
        public JsonElement Data { get; set; }

        public ApiPlayerUpdate(PlayerUpdateView db)
        {
            UpdateId = db.UpdateId;
            PlayerId = db.PlayerId;
            FirstSeen = db.FirstSeen;
            LastSeen = db.LastSeen;
            Data = db.Data;
        }
    }
}