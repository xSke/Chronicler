using System;
using SIBR.Storage.Data.Utils;

namespace SIBR.Storage.Data.Models
{
    public interface IPlayerData: IJsonData
    {
        public Guid PlayerId { get; }
        
        public PlayerStars Stars =>
            PlayerStars.CalculateStars(Data);
    }
}