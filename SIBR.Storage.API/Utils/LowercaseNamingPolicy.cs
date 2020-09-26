using System.Text.Json;

namespace SIBR.Storage.API.Utils
{
    public class LowercaseNamingPolicy : JsonNamingPolicy
    {
        public override string ConvertName(string name)
        {
            return name.ToLowerInvariant();
        }
    }
}