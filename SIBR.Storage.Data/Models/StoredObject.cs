using System;
using Newtonsoft.Json.Linq;
using SIBR.Storage.Data.Utils;

namespace SIBR.Storage.Data.Models
{
    public class StoredObject
    {
        public Guid Hash { get; set; }
        public JToken Data { get; set; }

        public StoredObject(JToken data)
        {
            Hash = SibrHash.HashAsGuid(data);
            Data = data;
        }
    }
}