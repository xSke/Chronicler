using NodaTime;
using SIBR.Storage.Data.Query;

namespace SIBR.Storage.API.Models
{
    public interface IUpdateQuery
    {
        public Instant? Before { get; set; }
        public Instant? After { get; set; }
        public SortOrder Order { get; set; }
        public PageToken Page { get; }
        public int? Count { get; set; }
    }
}