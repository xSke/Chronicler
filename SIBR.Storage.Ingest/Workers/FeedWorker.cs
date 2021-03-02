using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NodaTime;
using NodaTime.Text;
using SIBR.Storage.Data;
using SIBR.Storage.Data.Models;

namespace SIBR.Storage.Ingest
{
    public class FeedWorker: IntervalWorker
    {
        private const int FetchLimit = 500;
        private static readonly TimeSpan FetchDelay = TimeSpan.FromMilliseconds(500);
        
        private readonly HttpClient _client;
        private readonly FeedStore _feedStore;
        private readonly Database _db;
        
        public FeedWorker(IServiceProvider services, IntervalWorkerConfiguration config) : base(services, config)
        {
            Blocking = true;
            
            _client = services.GetRequiredService<HttpClient>();
            _feedStore = services.GetRequiredService<FeedStore>();
            _db = services.GetRequiredService<Database>();
        }
        
        protected override async Task RunInterval()
        {
            await using var conn = await _db.Obtain();
            
            while (true)
            {
                await using (var tx = await conn.BeginTransactionAsync())
                {
                    var lastItem = await _feedStore.GetLatestFeedItem(conn);
                    var lastTimestamp = lastItem?.Timestamp;

                    var feedUrl = GetFeedUrl(lastTimestamp, FetchLimit);
                    var feedItems = await GetFeedItems(feedUrl);
                    _logger.Information("Fetched {ItemCount} new feed items starting at {BeforeFilter}", feedItems.Count,
                        lastTimestamp);

                    if (feedItems.Count == 0)
                        break;

                    if (feedItems.Count == 1 && feedItems.First().Id == lastItem?.Id)
                        break;

                    await _feedStore.SaveFeedItems(conn, feedItems);

                    await tx.CommitAsync();
                }

                await Task.Delay(FetchDelay);
            }
        }

        private string GetFeedUrl(Instant? start, int limit)
        {
            if (start == null) 
                return $"https://www.blaseball.com/database/feed/global?limit={limit}&sort=1";
            
            var isoTime = InstantPattern.ExtendedIso.Format(start.Value);
            return $"https://www.blaseball.com/database/feed/global?limit={limit}&sort=1&start={isoTime}";
        }

        private async Task<List<FeedItem>> GetFeedItems(string url)
        {
            var jsonStr = await _client.GetStringAsync(url);
            var arr = JsonConvert.DeserializeObject<JArray>(jsonStr,
                new JsonSerializerSettings {DateParseHandling = DateParseHandling.None});
            return arr!.Select(itemJson =>
            {
                var id = Guid.Parse(itemJson["id"].Value<string>());
                var timestamp = InstantPattern.ExtendedIso.Parse(itemJson["created"].Value<string>()).Value;
                return new FeedItem {Id = id, Timestamp = timestamp, Data = (JObject) itemJson};
            }).ToList();
        }
    }
}