using System;

namespace SIBR.Storage.Data.Models
{
    public interface ITeamData: IJsonData
    {
        public Guid TeamId { get; }
    }
}