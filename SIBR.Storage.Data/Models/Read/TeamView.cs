using System;
using System.Text.Json;
using NodaTime;
using SIBR.Storage.Data.Query;

namespace SIBR.Storage.Data.Models
{
    public class TeamView: ITeamData, IPaginatedView
    {
        public Guid UpdateId { get; }
        public Guid TeamId { get; }
        public Instant Timestamp { get; }
        public JsonElement Data { get; }
        
        public PageToken NextPage => new PageToken(Timestamp, TeamId);
    }
}