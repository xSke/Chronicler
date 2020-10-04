using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using NodaTime;
using NodaTime.Serialization.SystemTextJson;
using Serilog;
using SIBR.Storage.Data;
using SIBR.Storage.Data.Models;
using SIBR.Storage.Data.Utils;

namespace SIBR.Storage.CLI.Export
{
    public class FlatFileExport
    {
        private readonly ILogger _logger;
        private readonly UpdateStore _updateStore;
        private readonly GameUpdateStore _gameUpdateStore;
        private readonly GameStore _gameStore;

        private readonly JsonSerializerOptions _jsonOptions =
            new JsonSerializerOptions
            {
                IgnoreNullValues = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            }.ConfigureForNodaTime(DateTimeZoneProviders.Tzdb);

        public FlatFileExport(UpdateStore updateStore, ILogger logger, GameUpdateStore gameUpdateStore,
            GameStore gameStore)
        {
            _updateStore = updateStore;
            _gameUpdateStore = gameUpdateStore;
            _gameStore = gameStore;
            _logger = logger.ForContext<FlatFileExport>();
        }

        public async Task Run(ExportOptions opts)
        {
            _logger.Information("Starting export to {OutDir}...", opts.OutDir);

            var sw = new Stopwatch();
            sw.Start();
            await ExportByEntityId(opts, UpdateType.Player, Path.Join(opts.OutDir, "players"));
            await ExportByEntityId(opts, UpdateType.Team, Path.Join(opts.OutDir, "teams"));
            await Task.WhenAll(
                ExportByEntityId(opts, UpdateType.Season, Path.Join(opts.OutDir, "seasons")),
                ExportByEntityId(opts, UpdateType.League, Path.Join(opts.OutDir, "teams", "leagues")),
                ExportByEntityId(opts, UpdateType.Subleague, Path.Join(opts.OutDir, "teams", "subleagues")),
                ExportByEntityId(opts, UpdateType.Division, Path.Join(opts.OutDir, "teams", "divisions")),
                ExportByEntityId(opts, UpdateType.Standings, Path.Join(opts.OutDir, "teams", "standings")),
                ExportByEntityId(opts, UpdateType.Tiebreakers, Path.Join(opts.OutDir, "teams", "tiebreakers")),
                ExportAllByDay(opts, UpdateType.Tributes, Path.Join(opts.OutDir, "tributes", "tributes-")),
                ExportAllByDay(opts, UpdateType.Idols, Path.Join(opts.OutDir, "idols", "idols-")),
                ExportAllByDay(opts, UpdateType.GlobalEvents, Path.Join(opts.OutDir, "globalevents", "globalevents-")),
                ExportAllByDay(opts, UpdateType.Sim, Path.Join(opts.OutDir, "sim", "sim-")),
                ExportAllByDay(opts, UpdateType.OffseasonSetup, Path.Join(opts.OutDir, "offseasonsetup", "offseasonsetup-")),
                ExportAll(opts, UpdateType.Temporal, Path.Join(opts.OutDir, "temporal.json"))
            );
            // await ExportGameUpdates(opts, Path.Join(opts.OutDir, "games"));
            sw.Stop();

            _logger.Information("Finished export (took {Duration} total). Have a nice day~", sw.Elapsed);
        }

        private async Task ExportGameUpdates(ExportOptions opts, string outDir)
        {
            _logger.Information("Fetching games list...");
            var games = _gameStore.GetGames(new GameStore.GameQueryOptions
            {
                HasFinished = true
            });

            var chunks = games.Select(async game =>
            {
                await Task.Yield();

                var (season, day) = (((IGameData) game).Season, ((IGameData) game).Day);
                var filename = Path.Join(outDir, $"season{season}", $"day{day}", $"{game.GameId}.json");

                var updates = _gameUpdateStore.GetGameUpdates(new GameUpdateStore.GameUpdateQueryOptions
                {
                    Game = new[] {game.GameId}
                });

                _logger.Information("Writing game {GameId} (Season {Season} Day {Day}) to {Filename}", game.GameId,
                    season, day, filename);
                await WriteValues(opts, updates.Select(ToFileGameUpdate), filename);
            }).Buffer(20);

            await foreach (var chunk in chunks)
                await Task.WhenAll(chunk);
        }

        private async Task ExportAll(ExportOptions opts, UpdateType type, string filename)
        {
            _logger.Information("Exporting {EntityType} to {OutFile}", type, filename);

            var sw = new Stopwatch();
            sw.Start();
            var versions = _updateStore.ExportAllUpdatesGrouped(type);
            await WriteValues(opts, versions.Select(v => ToFileVersion(v, false)), filename);
            sw.Stop();

            _logger.Information("Done exporting {EntityType} (took {Duration})", type, sw.Elapsed);
        }

        private async Task ExportAllByDay(ExportOptions opts, UpdateType type, string outPrefix)
        {
            var sw = new Stopwatch();
            sw.Start();

            var versions = _updateStore.ExportAllUpdatesGrouped(type);
            await foreach (var group in versions.GroupByConsecutive(v => v.ObservationTimestamps.Min().InUtc().Date))
            {
                var filename = $"{outPrefix}{group.Key:R}.json";
                _logger.Information("Exporting {EntityType} to {OutFile}", type, filename);
                await WriteValues(opts, group.Select(v => ToFileVersion(v, false)).ToAsyncEnumerable(), filename);
            }

            sw.Stop();
            _logger.Information("Done exporting {EntityType} (took {Duration})", type, sw.Elapsed);
        }

        private async Task ExportAllByDayRaw(ExportOptions opts, UpdateType type, string outPrefix)
        {
            var sw = new Stopwatch();
            sw.Start();

            var versions = _updateStore.ExportAllUpdatesRaw(type);

            var versionId = 0;

            async IAsyncEnumerable<EntityVersionView> Versioned()
            {
                await foreach (var version in versions.GroupByConsecutive(u => u.Hash))
                {
                    var vers = new EntityVersionView
                    {
                        Version = versionId++,
                        Hash = version.Key,
                        Data = version.Values[0].Data,
                        Type = version.Values[0].Type,
                        EntityId = version.Values[0].EntityId ?? default,
                        ObservationTimestamps = version.Values.Select(u => u.Timestamp).ToArray(),
                        ObservationSources = version.Values.Select(u => u.SourceId).ToArray()
                    };
                    yield return vers;
                }
            }

            await foreach (var group in Versioned().GroupByConsecutive(v => v.ObservationTimestamps.Min().InUtc().Date))
            {
                var filename = $"{outPrefix}{group.Key:R}.json";
                _logger.Information("Exporting {EntityType} to {OutFile}", type, filename);
                await WriteValues(opts, group.ToAsyncEnumerable().Select(v => ToFileVersion(v, false)), filename);
                _logger.Information("Wrote {EntityType} to {OutFile}", type, filename);
            }

            sw.Stop();
            _logger.Information("Done exporting {EntityType} (took {Duration})", type, sw.Elapsed);
        }

        private async Task ExportByEntityId(ExportOptions opts, UpdateType type, string outDir)
        {
            _logger.Information("Exporting all of type {EntityType} to {OutDir}", type, outDir);

            var sw = new Stopwatch();
            sw.Start();
            var versions = _updateStore.ExportAllUpdatesGrouped(type);
            await WriteByEntityId(opts, outDir, versions);
            sw.Stop();

            _logger.Information("Done exporting {EntityType} (took {Duration})", type, sw.Elapsed);
        }

        private async Task WriteByEntityId(ExportOptions opts, string outDir, IAsyncEnumerable<EntityVersionView> versions)
        {
            await foreach (var version in versions.GroupByConsecutive(v => v.EntityId))
                await WriteVersionsWithEntityId(opts, version.Values, outDir);
        }

        private async Task WriteVersionsWithEntityId(ExportOptions opts, IReadOnlyCollection<EntityVersionView> versions,
            string outDir)
        {
            var entityId = versions.First().EntityId;
            var filename = Path.Join(outDir, $"{entityId}.json");
            _logger.Information("Exporting {EntityId} versions to {Filename}", entityId, filename);
            await WriteValues(opts, versions.ToAsyncEnumerable().Select(v => ToFileVersion(v, false)), filename);
        }

        private async Task WriteValues(ExportOptions opts, IAsyncEnumerable<object> values, string filename)
        {
            async Task WriteToStream(Stream stream)
            {
                await foreach (var value in values)
                {
                    var json = JsonSerializer.SerializeToUtf8Bytes(value, _jsonOptions);
                    await stream.WriteAsync(json);
                    await stream.WriteAsync(new byte[] {0x0a});
                }
            }

            Directory.CreateDirectory(Path.GetDirectoryName(filename));

            if (opts.Compress)
            {
                await using var stream = File.OpenWrite(filename + ".gz");
                await using var gz = new GZipStream(stream, CompressionLevel.Fastest);
                await WriteToStream(gz);
            }
            else
            {
                await using var stream = File.OpenWrite(filename);
                await WriteToStream(stream);
            }
        }

        private FileGameUpdate ToFileGameUpdate(GameUpdateView update) =>
            new FileGameUpdate
            {
                GameId = update.GameId,
                Timestamp = update.Timestamp,
                Hash = update.Hash,
                Data = update.Data
            };

        private FileVersion ToFileVersion(EntityVersionView version, bool includeObservations)
        {
            return new FileVersion
            {
                Id = version.EntityId == default ? (Guid?) null : version.EntityId,
                Hash = version.Hash,
                Version = version.Version,
                Data = version.Data,
                FirstSeen = version.ObservationTimestamps.First(),
                LastSeen = version.ObservationTimestamps.Last(),
                Observations = includeObservations
                    ? version.ObservationTimestamps
                        .Zip(version.ObservationSources)
                        .Select(pair => new Observation
                        {
                            Timestamp = pair.First,
                            Source = pair.Second
                        }).ToList()
                    : null
            };
        }

        public class FileVersion
        {
            public Guid? Id { get; set; }
            public int Version { get; set; }
            public Guid Hash { get; set; }
            public Instant? FirstSeen { get; set; }
            public Instant? LastSeen { get; set; }
            public List<Observation> Observations { get; set; }
            public JsonElement Data { get; set; }
        }

        public class FileGameUpdate
        {
            public Guid GameId { get; set; }
            public Instant? Timestamp { get; set; }
            public Guid Hash { get; set; }
            public JsonElement Data { get; set; }
        }

        public class Observation
        {
            public Instant Timestamp { get; set; }
            public Guid Source { get; set; }
        }

        public class ExportOptions
        {
            public string OutDir;
            public bool Compress;
            public bool IncludeObservations;
        }
    }
}