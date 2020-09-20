using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Serilog;

namespace SIBR.Storage.CLI.Import
{
    public abstract class S3FileImporter
    {
        protected readonly ILogger _logger;
        protected string FileFilter = "*";
        
        protected S3FileImporter(IServiceProvider services)
        {
            _logger = services.GetRequiredService<ILogger>().ForContext(GetType());
        }

        protected abstract Task ProcessFile(string filename, IAsyncEnumerable<JToken> entries);

        public async Task Run(S3ImportOptions options)
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
            using var reader = new StreamReader(gz);

            string line;
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
            }
        }
        
        protected DateTimeOffset? ExtractTimestamp(JToken obj)
        {
            var timestampToken = obj["clientMeta"]?["timestamp"];
            if (timestampToken == null)
                return null;

            return DateTimeOffset.FromUnixTimeMilliseconds(timestampToken.Value<long>());
        }

        protected DateTimeOffset? ExtractTimestampFromFilename(string filename, string regex)
        {
            var match = Regex.Match(filename, regex);
            if (match.Success)
                return DateTimeOffset.FromUnixTimeMilliseconds(long.Parse(match.Groups[1].Value));
            return null;
        }
    }
}