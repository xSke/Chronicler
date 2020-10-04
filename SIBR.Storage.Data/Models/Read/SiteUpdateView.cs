using System;
using NodaTime;
using SIBR.Storage.Data.Query;

namespace SIBR.Storage.Data.Models
{
    public class SiteUpdateView: IPaginatedView
    {
        public Guid Hash { get; set; }
        public string Path { get; set; }
        public Instant Timestamp { get; set; }
        public int Size { get; set; }
        
        public PageToken NextPage => new PageToken(Timestamp, Hash);
    }
}