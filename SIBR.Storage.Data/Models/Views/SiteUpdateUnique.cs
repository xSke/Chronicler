using System;
using NodaTime;

namespace SIBR.Storage.Data.Models
{
    public class SiteUpdateUnique
    {
        public Guid Hash { get; set; }
        public string Path { get; set; }
        public Instant Timestamp { get; set; }
    }
}