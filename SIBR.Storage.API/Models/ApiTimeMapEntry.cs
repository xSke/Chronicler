using NodaTime;
using SIBR.Storage.Data.Models;

namespace SIBR.Storage.API.Models
{
    public class ApiTimeMapEntry
    {
        public int Season { get; }
        public int Tournament { get; }
        public int Day { get; }
        public string Type { get; }
        
        public Instant StartTime { get; }
        public Instant? EndTime { get; }

        public ApiTimeMapEntry(TimeMapEntry db)
        {
            Season = db.Season;
            Tournament = db.Tournament;
            Day = db.Day;
            Type = db.Type;
            StartTime = db.StartTime;
            EndTime = db.EndTime;
        }
    }
}