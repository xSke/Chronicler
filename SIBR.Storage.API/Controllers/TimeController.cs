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
            var seasons = _time.GetTimeMap()
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
                        Days = timeMap.Max(v => v.Day),
                        
                        StartTime = timeMap.Min(v => v.StartTime),
                        SeasonStartTime = timeMap
                            .Where(v => v.Type == "season")
                            .Select(v => (Instant?) v.StartTime)
                            .DefaultIfEmpty().Min(),
                        PostseasonStartTime = timeMap
                            .Where(v => v.Type == "postseason")
                            .Select(v => (Instant?) v.StartTime)
                            .DefaultIfEmpty().Min(),
                        EndTime = timeMap.Any(v => v.EndTime == null)
                            ? null
                            : timeMap.Max(v => v.EndTime)
                    };
                });

            return Ok(new ApiResponse<ApiSeason>
            {
                Data = await seasons.ToListAsync()
            });
        }
    }
}