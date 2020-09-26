using System;
using System.Text.Json;
using NodaTime;

namespace SIBR.Storage.Data.Models
{
    public class Team
    {
        public Guid TeamId;
        public Instant Timestamp;
        public JsonElement Data;
    }
}