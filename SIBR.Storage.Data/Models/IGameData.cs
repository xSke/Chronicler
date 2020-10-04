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
    }
}