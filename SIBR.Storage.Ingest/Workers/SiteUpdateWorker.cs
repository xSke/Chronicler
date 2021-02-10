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

        private const string Index = "https://www.blaseball.com/";
        private static readonly string[] AllowedPrefixes =
        {
            "/", 
            "https://www.blaseball.com/", 
            "https://blaseball.com/",
            "https://d35iw2jmbg6ut8.cloudfront.net/"
        };

        public SiteUpdateWorker(IServiceProvider services, IntervalWorkerConfiguration config, Guid sourceId) :
            base(services, config)
        {
            _sourceId = sourceId;
            _client = services.GetRequiredService<HttpClient>();
            _siteUpdateStore = services.GetRequiredService<SiteUpdateStore>();
            _db = services.GetRequiredService<Database>();
            _clock = services.GetRequiredService<IClock>();
        }

        protected override async Task RunInterval()
        {
            var index = await FetchResource(new Uri(Index));
            if (index == null)
                return;

            var resourcePaths = ExtractResourcePathsFromPage(index);
            var resources = await Task.WhenAll(resourcePaths.Select(FetchResource));
            
            await using var conn = await _db.Obtain();
            await using var tx = await conn.BeginTransactionAsync();
            await _siteUpdateStore.SaveSiteUpdates(conn,  resources.Where(u => u != null).Concat(new[] {index}).ToList());
            await tx.CommitAsync();
        }

        private IEnumerable<Uri> ExtractResourcePathsFromPage(SiteUpdate index)
        {
            var doc = new HtmlDocument();
            doc.LoadHtml(Encoding.UTF8.GetString(index.Data));

            return doc.DocumentNode.SelectNodes("//script/@src | //link[@rel='stylesheet']/@href")
                .Select(node => node.GetAttributeValue("src", null) ?? node.GetAttributeValue("href", null))
                .Where(s => s != null)
                .Where(s => AllowedPrefixes.Any(s.StartsWith))
                .Select(ParseRelativeUri)
                .ToArray();
        }

        private async Task<SiteUpdate> FetchResource(Uri uri)
        {
            var response = await _client.GetAsync(uri);
            if (!response.IsSuccessStatusCode)
            {
                _logger.Warning("Got {StatusCode} response from {Url}, returning null", response.StatusCode, uri);
                return null;
            }

            var bytes = await response.Content.ReadAsByteArrayAsync();

            var lastModifiedDto = response.Content.Headers.LastModified;
            var lastModified = lastModifiedDto != null
                ? Instant.FromDateTimeOffset(lastModifiedDto.Value)
                : (Instant?) null;

            var path = uri.AbsolutePath;
            var update = SiteUpdate.From(_sourceId, path, _clock.GetCurrentInstant(), bytes, lastModified);
            _logger.Information("- Fetched resource {Path} (hash {Hash})", uri, update.Hash);
            return update;
        }

        private Uri ParseRelativeUri(string url)
        {
            var uri = new Uri(url, UriKind.RelativeOrAbsolute);
            if (uri.IsAbsoluteUri)
                return uri;
            
            return new Uri(new Uri("https://www.blaseball.com/"), url);
        }
    }
}