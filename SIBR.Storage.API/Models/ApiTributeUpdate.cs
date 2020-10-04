using System;
using System.Collections.Generic;
using System.Linq;
using NodaTime;
using SIBR.Storage.Data.Models;

namespace SIBR.Storage.API.Models
{
    public class ApiTributeUpdate
    {
        public Guid UpdateId { get; set; }
        public Instant Timestamp { get; set; }
        public Dictionary<string, int> Players { get; set; }

        public ApiTributeUpdate(TributesUpdateView db)
        {
            UpdateId = db.UpdateId;
            Timestamp = db.Timestamp;

            Players = db.Players
                .Zip(db.Peanuts)
                .ToDictionary(
                    x => x.First.ToString(),
                    x => x.Second
                );
        }
    }
}