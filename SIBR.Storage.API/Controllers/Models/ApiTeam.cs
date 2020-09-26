using System;
using System.Text.Json;
using NodaTime;

namespace SIBR.Storage.API.Controllers.Models
{
    public class ApiTeam
    {
        public Guid Id { get; set; }
        public Instant LastUpdate { get; set; }
        public JsonElement Data { get; set; }
    }
}