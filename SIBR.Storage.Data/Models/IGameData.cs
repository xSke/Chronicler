using System;

namespace SIBR.Storage.Data.Models
{
    public interface IGameData: IJsonData
    {
        public Guid GameId { get; }
        
        public int Season => 
            Data.GetProperty("season").GetInt32();
        public int Day => 
            Data.GetProperty("day").GetInt32();
        public int Tournament
        {
            get
            {
                if (Data.TryGetProperty("tournament", out var prop))
                    return prop.GetInt32();
                return -1;
            }
        }
    }
}