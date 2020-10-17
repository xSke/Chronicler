using System;
using NodaTime;
using SIBR.Storage.Data.Query;

namespace SIBR.Storage.Data.Models
{
    public class RosterUpdateView: IPaginatedView
    {
        public Guid UpdateId { get; set; }
        public Guid PlayerId { get; set; }
        public Guid TeamId { get; set; }
        public PlayerView.TeamPosition Position { get; set; }
        public int RosterIndex { get; set; }
        public Instant FirstSeen { get; set; }
        public Instant LastSeen { get; set; }
        
        public PageToken NextPage => new PageToken(FirstSeen, UpdateId);
    }
}