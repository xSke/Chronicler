using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using Serilog;

namespace SIBR.Storage.Ingest
{
    public class EventStream
    {
        private const string StripPrefix = "data: ";
        
        private readonly HttpClient _client;
        private readonly ILogger _logger;

        public EventStream(HttpClient client, ILogger logger)
        {
            _client = client;
            _logger = logger;
        }

        public async Task OpenStream(string url, Action<string> callback)
        {
            while (true)
            {
                try
                {
                    _logger.Information("Connecting to stream URL {Url}", url);

                    var response = await _client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
                    await using var stream = await response.Content.ReadAsStreamAsync();
                    
                    _logger.Information("Connected to stream, receiving data");
                    
                    string str;
                    using var reader = new StreamReader(stream);
                    while ((str = await reader.ReadLineAsync()) != null)
                    {
                        if (string.IsNullOrWhiteSpace(str))
                            continue;

                        if (!str.StartsWith(StripPrefix))
                            continue;

                        callback(str.Substring(StripPrefix.Length));
                    }
                }
                catch (Exception e)
                {
                    _logger.Error(e, "Error while processing stream");
                }
            }
        }
    }
}