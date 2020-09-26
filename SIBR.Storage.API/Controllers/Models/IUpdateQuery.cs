using NodaTime;

namespace SIBR.Storage.API.Controllers.Models
{
    public interface IUpdateQuery
    {
        public Instant? Before { get; set; }
        public Instant? After { get; set; }
        public ResultOrder Order { get; set; }
        public int? Count { get; set; }
        
        public enum ResultOrder
        {
            Asc,
            Desc
        }
    }
}