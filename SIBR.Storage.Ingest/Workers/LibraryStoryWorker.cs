using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using SIBR.Storage.Data;
using SIBR.Storage.Data.Models;
using SIBR.Storage.Ingest.Utils;

namespace SIBR.Storage.Ingest
{
    public class LibraryStoryWorker: IntervalWorker
    {
        private readonly Guid _sourceId;
        private readonly HttpClient _client;
        private readonly Database _db;
        private readonly UpdateStore _updateStore;
        
        public LibraryStoryWorker(IServiceProvider services, IntervalWorkerConfiguration config, Guid sourceId) : base(services, config)
        {
            _sourceId = sourceId;
            _client = services.GetRequiredService<HttpClient>();
            _db = services.GetRequiredService<Database>();
            _updateStore = services.GetRequiredService<UpdateStore>();
        }

        protected override async Task RunInterval()
        {
            var storyIds = await GetStoryIds();

            var updates = await Task.WhenAll(storyIds.Select(FetchStory));
            await using var conn = await _db.Obtain();
            await _updateStore.SaveUpdates(conn, updates);
            
            _logger.Information("Saved {StoryCount} library stories", updates.Length);
        }

        private async Task<EntityUpdate> FetchStory(Guid id)
        {
            var (timestamp, json) = await _client.GetJsonAsync($"https://www.blaseball.com/database/feed/story?id={id}");
            return EntityUpdate.From(UpdateType.LibraryStory, _sourceId, timestamp, json, idOverride: id);
        }

        private async Task<IEnumerable<Guid>> GetStoryIds()
        {
            // we need to do this in a nicer way once we get JS resources into the db too?
            var (_, library) = await _client.GetJsonAsync("https://raw.githubusercontent.com/xSke/blaseball-site-files/main/data/library.json");

            var storyIds = new List<Guid>();
            foreach (var book in library)
            foreach (var chapter in book["chapters"]!)
            {
                var redacted = chapter["redacted"]?.ToObject<bool>() ?? false;
                if (!redacted)
                    storyIds.Add(chapter["id"]!.ToObject<Guid>());
            }

            return storyIds;
        }
    }
}