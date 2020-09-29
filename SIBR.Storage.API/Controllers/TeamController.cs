using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using NodaTime;
using SIBR.Storage.API.Controllers.Models;
using SIBR.Storage.API.Utils;
using SIBR.Storage.Data;
using SIBR.Storage.Data.Models;

namespace SIBR.Storage.API.Controllers
{
    [Route("api/teams"), ApiController]
    public class TeamController: ControllerBase
    {
        private readonly TeamUpdateStore _store;

        public TeamController(TeamUpdateStore store)
        {
            _store = store;
        }
        
        [Route("")]
        public async Task<IEnumerable<ApiTeam>> GetTeams([FromQuery] TeamUpdateQuery query)
        {
            return (await _store.GetTeams()).Select(ToApiTeam);
        }

        private ApiTeam ToApiTeam(Team team) =>
            new ApiTeam
            {
                Id = team.TeamId,
                LastUpdate = team.Timestamp,
                Data = team.Data
            };

        [Route("updates")]
        public IAsyncEnumerable<ApiTeamUpdate> GetTeamUpdates([FromQuery] TeamUpdateQuery query)
        {
            return _store.GetTeamUpdates(new TeamUpdateStore.TeamUpdateQueryOpts
            {
                Teams = query.Team,
                After = query.After,
                Before = query.Before,
                Count = query.Count ?? 100,
                Reverse = query.Order == IUpdateQuery.ResultOrder.Desc,
                PageUpdateId = query.Page
            }).Select(ToApiTeamUpdate);
        }

        private ApiTeamUpdate ToApiTeamUpdate(TeamUpdate update) =>
            new ApiTeamUpdate
            {
                UpdateId = update.UpdateId,
                TeamId = update.TeamId,
                FirstSeen = update.FirstSeen,
                LastSeen = update.LastSeen,
                Hash = update.Hash,
                Data = update.Data
            };

        public class TeamUpdateQuery: IUpdateQuery
        {
            [BindProperty(BinderType = typeof(CommaSeparatedBinder))]
            public Guid[] Team { get; set; }
            public Instant? Before { get; set; }
            public Instant? After { get; set; }
            public IUpdateQuery.ResultOrder Order { get; set; }
            public Guid? Page { get; set; }
            [Range(1, 250)] public int? Count { get; set; }
        }
    }
}