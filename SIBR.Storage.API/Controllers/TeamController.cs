using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CsvHelper;
using Microsoft.AspNetCore.Mvc;
using NodaTime;
using SIBR.Storage.API.Models;
using SIBR.Storage.API.Utils;
using SIBR.Storage.Data;
using SIBR.Storage.Data.Models;
using SIBR.Storage.Data.Query;

namespace SIBR.Storage.API.Controllers
{
    [Route("v{version:apiVersion}"), ApiController]
    [ApiVersion("1.0")]
    public class TeamController: ControllerBase
    {
        private readonly TeamUpdateStore _store;

        public TeamController(TeamUpdateStore store)
        {
            _store = store;
        }

        [Route("roster/updates")]
        public async Task<IActionResult> GetRosterUpdates([FromQuery] RosterUpdateQuery query)
        {
            var rosterUpdates = await _store.GetRosterUpdates(new TeamUpdateStore.RosterUpdateQueryOpts
            {
                Before = query.Before,
                After = query.After,
                Count = query.Count ?? 100,
                Order = query.Order,
                Page = query.Page,
                Players = query.Player,
                Teams = query.Team
            }).ToListAsync();

            return Ok(new ApiResponsePaginated<ApiRosterUpdate>
            {
                Data = rosterUpdates.Select(u => new ApiRosterUpdate(u)),
                NextPage = rosterUpdates.LastOrDefault()?.NextPage
            });
        }
        
        [Route("teams")]
        public async Task<IActionResult> GetTeams([FromQuery] TeamQuery query) {
            var teams = await _store.GetTeams();
            
            if (query.Format == ResponseFormat.Csv)
            {
                await using var sw = new StringWriter();
                await using (var w = new CsvWriter(sw, CultureInfo.InvariantCulture))
                {
                    w.Configuration.TypeConverterCache.AddConverter<bool>(new LowercaseBooleanConverter());
                    await w.WriteRecordsAsync(teams.Select(x => new CsvTeam(x)));
                }

                return Ok(sw.ToString());
            }
            
            return Ok(new ApiResponse<ApiTeam>
            {
                Data = teams.Select(u => new ApiTeam(u))
            });
        }

        [Route("teams/updates")]
        public async Task<IActionResult> GetTeamUpdates([FromQuery] TeamUpdateQuery query)
        {
            var updates = await _store.GetTeamUpdates(new TeamUpdateStore.TeamUpdateQueryOpts
            {
                Teams = query.Team,
                After = query.After,
                Before = query.Before,
                Count = query.Count ?? 100,
                Order = query.Order,
                Page = query.Page
            }).ToListAsync();

            return Ok(new ApiResponsePaginated<ApiTeamUpdate>
            {
                Data = updates.Select(u => new ApiTeamUpdate(u)),
                NextPage = updates.LastOrDefault()?.NextPage
            });
        }

        public class TeamUpdateQuery: IUpdateQuery
        {
            [BindProperty(BinderType = typeof(CommaSeparatedBinder))]
            public Guid[] Team { get; set; }
            public Instant? Before { get; set; }
            public Instant? After { get; set; }
            public SortOrder Order { get; set; }
            public PageToken Page { get; set; }
            [Range(1, 250)] public int? Count { get; set; }
        }

        public class TeamQuery
        {
            public ResponseFormat Format { get; set; }
        }
        
        public class RosterUpdateQuery: IUpdateQuery
        {
            [BindProperty(BinderType = typeof(CommaSeparatedBinder))]
            public Guid[] Team { get; set; }
            [BindProperty(BinderType = typeof(CommaSeparatedBinder))]
            public Guid[] Player { get; set; }
            public Instant? Before { get; set; }
            public Instant? After { get; set; }
            public SortOrder Order { get; set; }
            public PageToken Page { get; }
            [Range(1, 1000)] public int? Count { get; set; }
        }
    }
}