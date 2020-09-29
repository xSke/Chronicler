using System;
using System.Collections.Generic;
using NodaTime;

namespace SIBR.Storage.API.Controllers.Models
{
    public class ApiTributeUpdate
    {
        public Guid UpdateId { get; set; }
        public Instant Timestamp { get; set; }
        public Dictionary<string, int> Players { get; set; }
    }
}