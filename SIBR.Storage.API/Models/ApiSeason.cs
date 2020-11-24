using NodaTime;

namespace SIBR.Storage.API.Models
{
    public class ApiSeason
    {
        public int Season { get; set; }
        public int Tournament { get; set; }
        public Instant StartTime { get; set; }
        public Instant? SeasonStartTime { get; set; }
        public Instant? PostseasonStartTime { get; set; }
        public Instant? EndTime { get; set; }
        public int Days { get; set; }
    }
}