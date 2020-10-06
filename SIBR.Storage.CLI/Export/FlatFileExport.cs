using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
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
            
            var streamExport = ExportAllByDayRaw(opts, UpdateType.Stream, Path.Join(opts.OutDir, "stream", "stream-"));
            
            await ExportByEntityId(opts, UpdateType.Player, Path.Join(opts.OutDir, "players"));
            await ExportByEntityId(opts, UpdateType.Team, Path.Join(opts.OutDir, "teams"));

            await Task.WhenAll(
                ExportByEntityId(opts, UpdateType.Season, Path.Join(opts.OutDir, "league", "seasons")),
                ExportByEntityId(opts, UpdateType.League, Path.Join(opts.OutDir, "league", "leagues")),
                ExportByEntityId(opts, UpdateType.Subleague, Path.Join(opts.OutDir, "league", "subleagues")),
                ExportByEntityId(opts, UpdateType.Division, Path.Join(opts.OutDir, "league", "divisions")),
                ExportByEntityId(opts, UpdateType.Standings, Path.Join(opts.OutDir, "league", "standings")),
                ExportByEntityId(opts, UpdateType.Tiebreakers, Path.Join(opts.OutDir, "league", "tiebreakers"))
            );

            await Task.WhenAll(
                ExportByEntityId(opts, UpdateType.SeasonStatsheet, Path.Join(opts.OutDir, "statsheets", "season")),
                ExportByEntityId(opts, UpdateType.GameStatsheet, Path.Join(opts.OutDir, "statsheets", "game")),
                ExportByEntityId(opts, UpdateType.TeamStatsheet, Path.Join(opts.OutDir, "statsheets", "team")),
                ExportByEntityId(opts, UpdateType.PlayerStatsheet, Path.Join(opts.OutDir, "statsheets", "player"))
            );
            
            await Task.WhenAll(
                ExportAllByDay(opts, UpdateType.Tributes, Path.Join(opts.OutDir, "tributes", "tributes-")),
                ExportAllByDay(opts, UpdateType.Idols, Path.Join(opts.OutDir, "idols", "idols-")),
                ExportAllByDay(opts, UpdateType.OffseasonSetup, Path.Join(opts.OutDir, "election", "offseasonsetup-")),
                ExportAllByDay(opts, UpdateType.GlobalEvents, Path.Join(opts.OutDir, "globalevents", "globalevents-")),
                ExportAll(opts, UpdateType.Sim, Path.Join(opts.OutDir, "sim", "sim.json")),
                ExportAll(opts, UpdateType.Temporal, Path.Join(opts.OutDir, "sim", "temporal.json"))
            );

            await Task.WhenAll(
                streamExport,
                ExportGameUpdates(opts, Path.Join(opts.OutDir, "games"))
            );
            
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
            var versions = GroupVersion(_updateStore.ExportAllUpdatesGrouped(type));
            await WriteValues(opts, versions.Select(v => ToFileVersion(v, false)), filename);
            sw.Stop();

            _logger.Information("Done exporting {EntityType} (took {Duration})", type, sw.Elapsed);
        }

        private async Task ExportAllByDay(ExportOptions opts, UpdateType type, string outPrefix)
        {
            var sw = new Stopwatch();
            sw.Start();

            var versions =  GroupVersion(_updateStore.ExportAllUpdatesGrouped(type));
            await foreach (var group in versions.GroupByConsecutive(v => v.ObservationTimestamps.Min().InUtc().Date))
            {
                var filename = $"{outPrefix}{group.Key:R}.json";
                _logger.Information("Exporting {EntityType} to {OutFile}", type, filename);
                await WriteValues(opts, group.Select(v => ToFileVersion(v, false)).ToAsyncEnumerable(), filename);
            }

            sw.Stop();
            _logger.Information("Done exporting {EntityType} (took {Duration})", type, sw.Elapsed);
        }

        private IAsyncEnumerable<EntityVersionWithObservationsView> GroupVersion(IAsyncEnumerable<EntityUpdateView> inputGrouped)
        {
            var versionNumbers = new ConcurrentDictionary<(UpdateType, Guid), int>();
            return inputGrouped
                .GroupByConsecutive(update => (update.Type, update.EntityId, update.Hash))
                .Select(versionGroup =>
                {
                    return new EntityVersionWithObservationsView
                    {
                        Type = versionGroup.Key.Type,
                        EntityId = versionGroup.Key.EntityId ?? default,
                        Hash = versionGroup.Key.Hash,
                        Data = versionGroup.Values[0].Data,
                        ObservationTimestamps = versionGroup.Values.Select(upd => upd.Timestamp).ToArray(),
                        ObservationSources = versionGroup.Values.Select(upd => upd.SourceId).ToArray(),
                        Version = versionNumbers.AddOrUpdate((versionGroup.Key.Type, versionGroup.Key.EntityId ?? default), _ => 1, (_, last) => last + 1)
                    };
                });
        }

        private async Task ExportAllByDayRaw(ExportOptions opts, UpdateType type, string outPrefix)
        {
            var sw = new Stopwatch();
            sw.Start();

            var updates = _updateStore.ExportAllUpdatesRaw(type);
            await foreach (var group in updates.GroupByConsecutive(v => v.Timestamp.InUtc().Date))
            {
                var filename = $"{outPrefix}{group.Key:R}.json";
                _logger.Information("Exporting {EntityType} to {OutFile}", type, filename);

                await WriteValues(opts, group
                    .ToAsyncEnumerable()
                    .Select(g => new RawUpdateObject
                {
                    Timestamp = g.Timestamp,
                    SourceId = g.SourceId,
                    UpdateId = g.UpdateId,
                    Data = g.Data,
                    Hash = g.Hash
                }), filename);
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
            var versions = GroupVersion(_updateStore.ExportAllUpdatesGrouped(type));
            await WriteByEntityId(opts, outDir, versions);
            sw.Stop();

            _logger.Information("Done exporting {EntityType} (took {Duration})", type, sw.Elapsed);
        }

        private async Task WriteByEntityId(ExportOptions opts, string outDir, IAsyncEnumerable<EntityVersionWithObservationsView> versions)
        {
            await foreach (var version in versions.GroupByConsecutive(v => v.EntityId))
                await WriteVersionsWithEntityId(opts, version.Values, outDir);
        }

        private async Task WriteVersionsWithEntityId(ExportOptions opts, IReadOnlyCollection<EntityVersionWithObservationsView> versions,
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

        private FileVersion ToFileVersion(EntityVersionWithObservationsView version, bool includeObservations)
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

        public class RawUpdateObject
        {
            public Instant Timestamp { get; set; }
            public Guid Hash { get; set; }
            public JsonElement Data { get; set; }
            public Guid UpdateId { get; set; }
            public Guid SourceId { get; set; }
        }
    }
}