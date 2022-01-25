using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using NodaTime;
using PusherClient;
using SIBR.Storage.Data;
using SIBR.Storage.Data.Models;
using SIBR.Storage.Ingest.Utils;

namespace SIBR.Storage.Ingest
{
    public class PusherWorker: BaseWorker
    {
        private readonly Pusher _pusher;
        private readonly Database _db;
        private readonly UpdateStore _updateStore;
        private readonly HttpClient _client;
        private readonly PusherEventStore _pusherEventStore;
        private readonly IClock _clock;
        private readonly Guid _sourceId;

        public PusherWorker(IServiceProvider services, Guid sourceId) : base(services)
        {
            _sourceId = sourceId;
            _pusher = new Pusher("ddb8c477293f80ee9c63", new PusherOptions
            {
                Cluster = "us3",
            });
            _db = services.GetRequiredService<Database>();
            _updateStore = services.GetRequiredService<UpdateStore>();
            _client = services.GetRequiredService<HttpClient>();
            _pusherEventStore = services.GetRequiredService<PusherEventStore>();
            _clock = services.GetRequiredService<IClock>();
        }

        protected override async Task Run()
        {
            _pusher.ConnectionStateChanged += (_, state) =>
            {
                _logger.Information("Pusher connection state changed: {PusherConnectionState}", state);
            };

            _pusher.Subscribed += (_, channel) =>
            {
                _logger.Information("Pusher subscribed to channel: {PusherChannelName}", channel.Name);
            };
            
            await _pusher.ConnectAsync();
            await _pusher.SubscribeAsync("sim-data");
            await _pusher.SubscribeAsync("temporal");
            await _pusher.SubscribeAsync("ticker");
            
            _pusher.BindAll(GlobalHandler);
            _pusher.Bind("sim-data", WrapHandler(SimDataHandler));
            _pusher.Bind("temporal-message", WrapHandler(TemporalHandler));
            _pusher.Bind("ticker-message", WrapHandler(TickerHandler));

            // Wait forever
            await SubscribeLoop();
        }

        private async Task SubscribeToGames(int season, int day, string sim)
        {
            var (_, games) = await _client.GetJsonAsync($"https://api.blaseball.com/database/games?season={season}&day={day}");
            var filtered = games.Where(g => g["sim"].Value<string>() == sim);
            var tasks = filtered.Select(g => _pusher.SubscribeAsync($"game-feed-{g["id"]?.Value<string>()}"));
            await Task.WhenAll(tasks);
        }
        
        private async Task SubscribeLoop()
        {
            while (true)
            {
                try
                {
                    await using var conn = await _db.Obtain();
                    var latest = await _updateStore.GetLatestUpdate(conn, UpdateType.Sim);
                    var season = latest.Data["season"]!.ToObject<int>();
                    var day = latest.Data["day"]!.ToObject<int>();
                    var sim = latest.Data["id"]!.ToObject<string>();

                    await SubscribeToGames(season, day, sim);
                    await SubscribeToGames(season, day + 1, sim);
                }
                catch (Exception e)
                {
                    _logger.Error(e, "Error in Pusher subscribe loop");
                }

                await Task.Delay(TimeSpan.FromMinutes(2));
            }
        }

        private void GlobalHandler(string _, PusherEvent evt)
        {
            // Get timestamp earliest possible (outside async)
            var timestamp = _clock.GetCurrentInstant();

            async Task Inner()
            {
                await Task.Yield();
                
                try
                {
                    var data = TryDecode(evt.Data);
                    await _pusherEventStore.SaveEvent(evt.ChannelName, evt.EventName, timestamp, data, evt.Data);
                }
                catch (Exception e)
                {
                    _logger.Error(e, "Error handling Pusher event {ChannelName}/{EventName}", evt.ChannelName, evt.EventName);
                }
            }

            // Fork
            var __ = Inner();
        }
        
        private JToken TryDecode(string data)
        {
            try
            {
                if (data.StartsWith("\""))
                    data = JsonConvert.DeserializeObject<string>(data);

                var payload = JsonConvert.DeserializeObject<JToken>(data);

                if (payload is JObject jo && jo.Count == 1 && jo.TryGetValue("message", out var message))
                {
                    var inner = message.Value<string>();
                    if (inner.StartsWith("\""))
                        inner = JsonConvert.DeserializeObject<string>(inner);

                    return DecodeCompressedPayload(inner);
                }

                return payload;
            }
            catch (Exception e)
            {
                _logger.Error(e, "Error decoding Pusher {Payload}", data);
            }

            return null;
        }

        private JToken DecodeCompressedPayload(string payload)
        {
            var bytes = Convert.FromBase64String(payload);
            using var ms = new MemoryStream(bytes);
            using var gs = new GZipStream(ms, CompressionMode.Decompress);
            using var r = new StreamReader(gs, Encoding.UTF8);
            return JsonConvert.DeserializeObject<JToken>(r.ReadToEnd());
        }

        private Action<PusherEvent> WrapHandler(Func<JToken, Task> inner)
        {
            async Task Inner(PusherEvent evt)
            {
                try
                {
                    _logger.Information("Received Pusher event {EventName} on {ChannelName}: {Data}", evt.EventName,
                        evt.ChannelName, evt.Data);

                    var data = TryDecode(evt.Data);
                    await inner(data);
                }
                catch (Exception e)
                {
                    _logger.Error(e, "Error handling Pusher event {EventName}", evt.EventName);
                }

            }
            return evt =>
            {
                // Fork inner task
                var _ = Inner(evt);
            };
        }

        private async Task SimDataHandler(JToken obj)
        {
            var (timestamp, data) = await _client.GetJsonAsync("https://api.blaseball.com/database/simulationData");
            var update = EntityUpdate.From(UpdateType.Sim, _sourceId, timestamp, data);
            
            await using var conn = await _db.Obtain();
            await _updateStore.SaveUpdate(conn, update);
            _logger.Information("Pulled SimData based on Pusher event");
        }
        
        private async Task TemporalHandler(JToken obj)
        {
            var (timestamp, data) = await _client.GetJsonAsync("https://api.blaseball.com/api/temporal");
            var update = EntityUpdate.From(UpdateType.Temporal, _sourceId, timestamp, data);
            
            await using var conn = await _db.Obtain();
            await _updateStore.SaveUpdate(conn, update);
            _logger.Information("Pulled Temporal based on Pusher event");
        }
        
        private async Task TickerHandler(JToken obj)
        {
            var (timestamp, data) = await _client.GetJsonAsync("https://api.blaseball.com/database/globalEvents");
            var update = EntityUpdate.From(UpdateType.GlobalEvents, _sourceId, timestamp, data);
            
            await using var conn = await _db.Obtain();
            await _updateStore.SaveUpdate(conn, update);
            _logger.Information("Pulled GlobalEvents based on Pusher event");
        }
    }
}