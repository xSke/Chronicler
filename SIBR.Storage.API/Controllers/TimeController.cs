using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using NodaTime;
using SIBR.Storage.API.Models;
using SIBR.Storage.Data;

namespace SIBR.Storage.API.Controllers
{
    [ApiController]
    [Route("v{version:apiVersion}")]
    [ApiVersion("1.0")]
    public class SeasonController: ControllerBase
    {
        private readonly TimeStore _time;

        public SeasonController(TimeStore time)
        {
            _time = time;
        }
        
        [Route("time/map")]
        public async Task<IActionResult> GetTimeMap()
        {
            var timeMap = _time.GetTimeMap();

            return Ok(new ApiResponse<ApiTimeMapEntry>
            {
                Data = await timeMap.Select(v => new ApiTimeMapEntry(v))
                    .ToListAsync()
            });
        }

        [Route("time/seasons")]
        public async Task<IActionResult> GetSeasons()
        {
            var seasons = await _time.GetTimeMap()
                // special case the time before phase changes but the tournament value is there, this should still
                // count as the normal season, otherwise we get season 10  tourney 0 day 119 (too much)
                .GroupBy(v => (v.Season, v.Day < 100 ? v.Tournament : -1)) 
                .SelectAwait(async group =>
                {
                    var timeMap = await group.ToListAsync();
                    
                    return new ApiSeason
                    {
                        Season = group.Key.Season,      
                        Tournament = group.Key.Item2,
                        Days = timeMap.Max(v => v.Day) + 1,
                        
                        StartTime = timeMap.Min(v => v.StartTime),
                        SeasonStartTime = timeMap
                            .Where(v => v.Type == "season")
                            .Select(v => (Instant?) v.StartTime)
                            .DefaultIfEmpty().Min(),
                        PostseasonStartTime = timeMap
                            .Where(v => v.Type == "postseason")
                            .Select(v => (Instant?) v.StartTime)
                            .DefaultIfEmpty().Min(),
                        
                        // End time is the end of the last game? which we know exists if there's a pre_election phase
                        EndTime = timeMap.FirstOrDefault(v => v.Type == "pre_election")?.StartTime
                    };
                }).ToListAsync();
                
            // Add Season 1 data
            seasons.Insert(0, new ApiSeason {
                Season = 0,
                Tournament = -1,
                Days = 111,
                StartTime = Instant.FromUtc(2020, 7, 20, 16, 0, 0),
                SeasonStartTime = Instant.FromUtc(2020, 7, 20, 16, 0, 0),
                PostseasonStartTime = Instant.FromUtc(2020, 7, 25, 13, 0, 0),
                EndTime = Instant.FromUtc(2020, 7, 26, 0, 30, 0),
            });
            
            // Fix Season 2 data
            var s2 = seasons.FirstOrDefault(s => s.Season == 1);
            if (s2 != null)
            {
                s2.StartTime = Instant.FromUtc(2020, 7, 27, 16, 0, 0);
                s2.SeasonStartTime = Instant.FromUtc(2020, 7, 27, 16, 0, 0);
            }

            return Ok(new ApiResponse<ApiSeason>
            {
                Data = seasons
            });
        }
    }
}