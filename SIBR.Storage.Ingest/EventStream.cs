using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Serilog;
using SIBR.Storage.Ingest.Utils;

namespace SIBR.Storage.Ingest
{
    public class EventStream
    {
        private const string StripPrefix = "data: ";
        private static readonly TimeSpan ReadTimeout = TimeSpan.FromSeconds(60);
        
        private readonly HttpClient _client;
        private readonly ILogger _logger;

        public EventStream(HttpClient client, ILogger logger)
        {
            _client = client;
            _logger = logger.ForContext<EventStream>();
        }

        public async Task OpenStream(string url, int streamId, Action<string> callback)
        {
            while (true)
            {
                try
                {
                    _logger.Information("Stream #{StreamId}: Connecting to stream URL {Url}", streamId, url);

                    var response = await _client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
                    await using var stream = await response.Content.ReadAsStreamAsync();

                    _logger.Information("Stream #{StreamId}: Connected to stream, receiving data", streamId);

                    using var reader = new StreamReader(stream);

                    using var cts = new CancellationTokenSource();
                    while (true)
                    {
                        // Reset timeout for ReadLine cancellation token
                        cts.CancelAfter(ReadTimeout);
                        
                        var line = await reader.ReadLineAsync(cts.Token);
                        if (line == null)
                            break;

                        if (string.IsNullOrWhiteSpace(line))
                            continue;

                        if (!line.StartsWith(StripPrefix))
                            continue;

                        callback(line.Substring(StripPrefix.Length));
                    }
                    
                    _logger.Information("Stream #{StreamId}: Disconnected server-side, reconnecting...", streamId);
                }
                catch (TaskCanceledException)
                {
                    _logger.Warning("Stream #{StreamId}: Read timed out, reconnecting...", 
                        streamId, ReadTimeout);
                }
                catch (Exception e)
                {
                    _logger.Error(e, "Stream #{StreamId}: Error while reading", streamId);
                }
            }
        }
    }
}