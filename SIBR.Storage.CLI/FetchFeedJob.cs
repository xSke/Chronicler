using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NodaTime;
using NodaTime.Text;
using Serilog;
using SIBR.Storage.Data;
using SIBR.Storage.Data.Models;

namespace SIBR.Storage.CLI
{
    public class FetchFeedJob
    {
        private readonly Database _db;
        private readonly FeedStore _feedStore;
        private readonly HttpClient _client;
        private readonly ILogger _logger;

        public FetchFeedJob(Database db, FeedStore feedStore, HttpClient client, ILogger logger)
        {
            _db = db;
            _feedStore = feedStore;
            _client = client;
            _logger = logger.ForContext<FetchFeedJob>();
        }

        public async Task Run(int delayMs, Instant? start)
        {
            await using var conn = await _db.Obtain();
            HashSet<Guid> lastIds = null;

            while (true)
            {
                var url = GetFeedUrl(start, 1000);
                var feedItems = await GetFeedItems(url);
                if (feedItems.Count == 0)
                    break;
                
                if (lastIds != null && lastIds.SetEquals(feedItems.Select(i => i.Id)))
                    break;
                
                var saved = await _feedStore.SaveFeedItems(conn, feedItems);
                _logger.Information("Fetched {ItemCount} new feed items starting at {BeforeFilter} ({NewItems} new)", feedItems.Count,
                    start, saved);

                start = feedItems.Max(i => i.Timestamp) - Duration.FromMilliseconds(1);
                
                lastIds = feedItems.Select(i => i.Id).ToHashSet();
                await Task.Delay(TimeSpan.FromMilliseconds(delayMs));
            }
            
            _logger.Information("Done fetching feed!");
        }
        
        private string GetFeedUrl(Instant? start, int limit)
        {
            if (start == null) 
                return $"https://api.blaseball.com/database/feed/global?limit={limit}&sort=1";
            
            var isoTime = InstantPattern.ExtendedIso.Format(start.Value);
            return $"https://api.blaseball.com/database/feed/global?limit={limit}&sort=1&start={isoTime}";
        }

        private async Task<List<FeedItem>> GetFeedItems(string url)
        {
            // todo: move to utility, we might just need an API abstraction entirely somewhere? idk
            
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