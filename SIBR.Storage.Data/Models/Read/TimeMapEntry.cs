using NodaTime;

namespace SIBR.Storage.Data.Models
{
    public class TimeMapEntry
    {
        public int Season { get; set; }
        public int Tournament { get; set; }
        public int Day { get; set; }
        public string Type { get; set; }
        
        public Instant StartTime { get; set; }  
        public Instant? EndTime { get; set; }
    }
}