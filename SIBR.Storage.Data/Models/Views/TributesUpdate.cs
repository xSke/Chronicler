using System;
using NodaTime;

namespace SIBR.Storage.Data.Models
{
    public class TributesUpdate
    {
        public Guid UpdateId { get; set; }
        public Instant Timestamp { get; set; }
        public Guid[] Players { get; set; }
        public int[] Peanuts { get; set; }
    }
}