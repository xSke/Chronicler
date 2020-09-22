using System;
using Newtonsoft.Json.Linq;

namespace SIBR.Storage.Data.Models
{
    public interface IJsonHashedObject
    {
        public Guid Hash { get; }
        public JToken Data { get; }
    }
}