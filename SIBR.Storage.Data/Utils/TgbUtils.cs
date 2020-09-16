using System;
using Newtonsoft.Json.Linq;

namespace SIBR.Storage.Data.Utils
{
    public class TgbUtils
    {
        public static Guid GetId(JObject obj)
        {
            if (obj.ContainsKey("_id"))
                return obj["_id"]!.ToObject<Guid>();
            if (obj.ContainsKey("id"))
                return obj["id"]!.ToObject<Guid>();
            throw new ArgumentException("Could not find _id or id key on object");
        }
    }
}