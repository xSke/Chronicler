using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Testing;
using NodaTime;
using NodaTime.Serialization.SystemTextJson;
using Shouldly;
using Xunit;

namespace SIBR.Storage.API.Tests
{
    public class UnitTest1: IClassFixture<WebApplicationFactory<Startup>>
    {
        private readonly WebApplicationFactory<Startup> _factory;
        private readonly JsonSerializerOptions _opts;
        private readonly HttpClient _client;

        public UnitTest1(WebApplicationFactory<Startup> factory)
        {
            _factory = factory;
            _client = factory.CreateClient();
            _opts = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            }.ConfigureForNodaTime(DateTimeZoneProviders.Tzdb);
        }

        [Theory]
        [InlineData("v1/site/updates", "timestamp")]
        [InlineData("v1/games/updates", "timestamp")]
        [InlineData("v1/players/updates", "firstSeen")]
        [InlineData("v1/teams/updates", "firstSeen")]
        [InlineData("v1/tributes/updates", "timestamp")]
        [InlineData("v1/tributes/hourly", "timestamp")]
        public async Task TestResponsesInProperOrder(string endpoint, string sortProperty)
        {
            var ascending = await MakeRequest(endpoint);
            ascending.Data.ShouldNotBeEmpty();

            for (var i = 1; i < ascending.Data.Count; i++)
            {
                var lastTimestamp = ascending.Data[i - 1].Fields[sortProperty].GetDateTimeOffset();
                var thisTimestamp = ascending.Data[i].Fields[sortProperty].GetDateTimeOffset();
            
                thisTimestamp.ShouldBeGreaterThanOrEqualTo(lastTimestamp);
            }

            var descending = await MakeRequest(endpoint + "?order=desc");
            descending.Data.ShouldNotBeEmpty();
            
            for (var i = 1; i < descending.Data.Count; i++)
            {
                var lastTimestamp = descending.Data[i - 1].Fields[sortProperty].GetDateTimeOffset();
                var thisTimestamp = descending.Data[i].Fields[sortProperty].GetDateTimeOffset();
                
                thisTimestamp.ShouldBeLessThanOrEqualTo(lastTimestamp);
            }
        }

        [Theory]
        [InlineData("v1/site/updates", 10, 1)]
        [InlineData("v1/games/updates", 200, 25)]
        [InlineData("v1/players/updates", 200, 25)]
        [InlineData("v1/teams/updates", 50, 5)]
        [InlineData("v1/tributes/updates", 200, 25)]
        [InlineData("v1/tributes/hourly", 200, 25)]
        public async Task TestCount(string endpoint, int max, int step)
        {
            for (var i = step; i <= max; i += step)
                (await MakeRequest(endpoint + $"?count={i}")).Data.Count.ShouldBe(i);
        }

        private async Task<Response> MakeRequest(string url)
        {
            var data = await _client.GetStringAsync(url);
            return JsonSerializer.Deserialize<Response>(data, _opts);
        }

        public class Response
        {
            public string NextPage { get; set; }
            public List<ResponseObject> Data { get; set; }
        }

        public class ResponseObject
        {
            [JsonExtensionData]
            public Dictionary<string, JsonElement> Fields { get; set; }
        }
    }
}