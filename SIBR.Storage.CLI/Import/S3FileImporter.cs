using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NodaTime;
using Serilog;

namespace SIBR.Storage.CLI.Import
{
    public abstract class S3FileImporter: FileImporter
    {
        protected string FileFilter = "*";

        protected abstract Task ProcessFile(string filename, IAsyncEnumerable<JToken> entries);

        public override async Task Run(ImportOptions options)
        {
            _logger.Information("Importing data files from from {Directory}", options.Directory);

            foreach (var filename in Directory.EnumerateFiles(options.Directory, FileFilter))
            {
                _logger.Information("Processing {Filename}", filename);
                await ProcessFile(filename, ReadJsonGzLines(filename));
            }

            _logger.Information("Done!");
        }
        
        private async IAsyncEnumerable<JToken> ReadJsonGzLines(string file)
        {
            await using var stream = File.OpenRead(file);
            await using var gz = new GZipStream(stream, CompressionMode.Decompress);
            using var reader = new StreamReader(gz, bufferSize: 8192);

            using var json = new JsonTextReader(reader)
            {
                SupportMultipleContent = true
            };

            var serializer = new JsonSerializer();
            while (await json.ReadAsync())
            {
                var token = serializer.Deserialize<JToken>(json);
                if (token != null)
                    yield return token;
                else
                    // Early out of entire file if parse error since we're reading by line
                    break;
            }
            /*string line;
            while ((line = await reader.ReadLineAsync()) != null)
            {
                JToken token = null;
                try
                {
                    token = JToken.Parse(line);
                }
                catch (JsonReaderException e)
                {
                    _logger.Error(e, "Error parsing JSON value: {Contents}", line);
                }

                if (token != null)
                    yield return token;
                else
                    // Early out of entire file if parse error since we're reading by line
                    break;
            }*/
        }
        
        protected Instant? ExtractTimestamp(JToken obj)
        {
            var timestampToken = obj["clientMeta"]?["timestamp"];
            if (timestampToken == null)
                return null;

            return Instant.FromUnixTimeMilliseconds(timestampToken.Value<long>());
        }

        protected Instant? ExtractTimestampFromFilename(string filename, string regex)
        {
            var match = Regex.Match(filename, regex);
            if (match.Success)
                return Instant.FromUnixTimeMilliseconds(long.Parse(match.Groups[1].Value));
            return null;
        }

        protected S3FileImporter(IServiceProvider services) : base(services)
        {
        }
    }
}