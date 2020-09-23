using System;
using Newtonsoft.Json.Linq;

namespace SIBR.Storage.Data.Models
{
    public interface IJsonObject
    {
        public Guid Hash { get; }
        public JToken Data { get; }
    }
}