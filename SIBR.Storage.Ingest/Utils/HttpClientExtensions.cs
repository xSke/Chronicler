using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NodaTime;

namespace SIBR.Storage.Ingest.Utils
{
    public static class HttpClientExtensions
    {
        public static async Task<(Instant Timestamp, JToken Data)> GetJsonAsync(this HttpClient client, string url)
        {
            var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();

            // Get timestamp after response, before content read
            var timestamp = SystemClock.Instance.GetCurrentInstant();

            var body = await response.Content.ReadAsStringAsync();
            var token = JsonConvert.DeserializeObject<JToken>(body);
            return (timestamp, token);
        }
    }
}