using System;
using NodaTime;
using SIBR.Storage.Data.Query;

namespace SIBR.Storage.Data.Models
{
    public class TributesUpdateView: IPaginatedView
    {
        public Guid UpdateId { get; set; }
        public Instant Timestamp { get; set; }
        public Guid[] Players { get; set; }
        public int[] Peanuts { get; set; }
        public PageToken NextPage => new PageToken(Timestamp, UpdateId);
    }
}