using System.Text.Json;

namespace SIBR.Storage.Data.Models
{
    public interface IJsonData
    {
        public JsonElement Data { get; }
    }
}