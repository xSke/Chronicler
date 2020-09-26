using System;
using NodaTime;

namespace SIBR.Storage.API.Controllers.Models
{
    public class ApiSiteUpdate
    {
        public Instant Timestamp { get; set; }
        public string Path { get; set; }
        public Guid Hash { get; set; }
        public int Size { get; set; }
        public string Download { get; set; }
    }
}