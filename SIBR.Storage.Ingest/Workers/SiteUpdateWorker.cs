using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using HtmlAgilityPack;
using Microsoft.Extensions.DependencyInjection;
using NodaTime;
using SIBR.Storage.Data;
using SIBR.Storage.Data.Models;

namespace SIBR.Storage.Ingest
{
    public class SiteUpdateWorker : IntervalWorker
    {
        private readonly Guid _sourceId;
        private readonly HttpClient _client;
        private readonly SiteUpdateStore _siteUpdateStore;
        private readonly Database _db;
        private readonly IClock _clock;

        public SiteUpdateWorker(IServiceProvider services, Guid sourceId) :
            base(services)
        {
            _sourceId = sourceId;
            _client = services.GetRequiredService<HttpClient>();
            _siteUpdateStore = services.GetRequiredService<SiteUpdateStore>();
            _db = services.GetRequiredService<Database>();
            _clock = services.GetRequiredService<IClock>();
            Interval = TimeSpan.FromMinutes(5);
        }

        protected override async Task RunInterval()
        {
            var index = await FetchResource("/");
            var resources = await Task.WhenAll(ExtractResourcePathsFromPage(index)
                .Select(FetchResource));
            
            await using var conn = await _db.Obtain();
            await using var tx = await conn.BeginTransactionAsync();
            await _siteUpdateStore.SaveSiteUpdates(conn, resources.Concat(new[] {index}).ToList());
            await tx.CommitAsync();
        }

        private IEnumerable<string> ExtractResourcePathsFromPage(SiteUpdate index)
        {
            var doc = new HtmlDocument();
            doc.LoadHtml(Encoding.UTF8.GetString(index.Data));

            return doc.DocumentNode.SelectNodes("//script/@src | //link[@rel='stylesheet']/@href")
                .Select(node => node.GetAttributeValue("src", null) ?? node.GetAttributeValue("href", null))
                .Where(s => s != null)
                // Exclude google ads and stuff, only local/relative paths
                .Where(s => s.StartsWith("/"))
                .ToArray();
        }

        private async Task<SiteUpdate> FetchResource(string path)
        {
            var resData = await _client.GetByteArrayAsync(new Uri(new Uri("https://www.blaseball.com/"), path));
            var update = new SiteUpdate(path, _clock.GetCurrentInstant(), resData);
            _logger.Information("- Fetched resource {Path} (hash {Hash})", path, update.Hash);
            return update;
        }
    }
}