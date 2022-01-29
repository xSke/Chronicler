using SIBR.Storage.Data.Models.Read;

namespace SIBR.Storage.API.Models
{
    public class ApiPusherEventRaw: ApiPusherEvent
    {
        public string Raw { get; set; }
        
        public ApiPusherEventRaw(PusherEvent db) : base(db)
        {
            Raw = db.Raw;
        }
    }
}