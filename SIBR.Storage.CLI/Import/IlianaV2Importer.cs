#nullable enable
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NodaTime;
using SIBR.Storage.Data;
using SIBR.Storage.Data.Models;
using SIBR.Storage.Data.Utils;

namespace SIBR.Storage.CLI.Import
{
    public class IlianaV2Importer: S3FileImporter
    {
        private readonly Guid _sourceId;
        private readonly Database _db;
        private readonly UpdateStore _updateStore;
        private readonly GameUpdateStore _gameUpdateStore;
        private readonly SibrHasher _hasher = new SibrHasher();
        
        public IlianaV2Importer(IServiceProvider services, Guid sourceId) : base(services)
        {
            _sourceId = sourceId;
            _db = services.GetRequiredService<Database>();
            _updateStore = services.GetRequiredService<UpdateStore>();
            _gameUpdateStore = services.GetRequiredService<GameUpdateStore>();
        }

        public override async Task Run(ImportOptions options)
        {
            await base.Run(options);
            
            await using var conn = await _db.Obtain();
            await _gameUpdateStore.UpdateSearchIndex(conn);
        }

        protected override async Task ProcessFile(string filename, IAsyncEnumerable<JToken> entries,
            ImportOptions options)
        {
            var updates = new List<EntityUpdate>();
            var gameUpdates = new List<GameUpdate>();
            var filtered = 0;

            await foreach (var entryToken in entries)
            {
                // TODO: do this in the first parsing step in the base class instead of "double-parsing"?
                var entry = entryToken.ToObject<DataEntry>()!;
                if (entry.Version != "2")
                {
                    _logger.Warning("Found invalid entry (no v2 tag), ignoring");
                    continue;
                }
                
                if (entry.Data is JObject {Count: 0})
                    // skip empty objects, we get those sometimes
                    continue;
                
                // TODO: be smarter about this and like, not read/parse the entire folder lmao
                var timestamp = entry.GetTimestamp();
                if (options.Before != null && timestamp > options.Before ||
                    options.After != null && timestamp < options.After)
                {
                    filtered++;
                    continue;
                }
                
                var id = entry.GetEntityId();
                if (GetSimpleUpdateType(entry.Endpoint) is UpdateType simpleType)
                {
                    updates.Add(EntityUpdate.From(simpleType, _sourceId, timestamp, entry.Data, _hasher, id));
                }
                else if (entry.Endpoint == "/events/streamData")
                {
                    updates.Add(EntityUpdate.From(UpdateType.Stream, _sourceId, timestamp, entry.Data, _hasher));
                    
                    var extract = TgbUtils.ExtractUpdatesFromStreamRoot(_sourceId, timestamp, (JObject) entry.Data!, _hasher);
                    updates.AddRange(extract.EntityUpdates);
                    gameUpdates.AddRange(extract.GameUpdates);
                }
            }
            
            if (filtered > 0)
                _logger.Information("{FilteredLineCount} lines filtered", filtered);

            if (updates.Count > 0 || gameUpdates.Count > 0)
            {
                _logger.Information("Parsed {UpdateCount} updates and {GameUpdateCount} game updates, saving...",
                    updates.Count, gameUpdates.Count);

                await using var conn = await _db.Obtain();
                await using var tx = await conn.BeginTransactionAsync();
                await _updateStore.SaveUpdates(conn, updates, false, false);
                await _gameUpdateStore.SaveGameUpdates(conn, gameUpdates, false, false);
                await tx.CommitAsync();
            }
        }

        public class DataEntry
        {
            [JsonProperty("version")] public string Version { get; set; }
            [JsonProperty("endpoint")] public string Endpoint { get; set; }
            [JsonProperty("id")] public string? Id { get; set; }
            [JsonProperty("time")] public double Time { get; set; }
            [JsonProperty("data")] public JToken? Data { get; set; }

            public Guid? GetEntityId() => Id == null ? TgbUtils.GenerateGuidFromString(Id) : (Guid?) null;
            public Instant GetTimestamp() => Instant.FromUnixTimeMilliseconds((long) (Time * 1000));
        }

        private UpdateType? GetSimpleUpdateType(string endpoint) => 
            endpoint switch
            {
                "/api/getIdols" => UpdateType.Idols,
                "/api/getTribute" => UpdateType.Tributes,
                "/database/players" => UpdateType.Player,
                "/database/offseasonSetup" => UpdateType.OffseasonSetup,
                "/database/globalEvents" => UpdateType.GlobalEvents,
                "/database/playerStatsheets" => UpdateType.PlayerStatsheet,
                "/database/teamStatsheets" => UpdateType.TeamStatsheet,
                "/database/gameStatsheets" => UpdateType.GameStatsheet,
                "/database/decreeResults" => UpdateType.DecreeResult,
                "/database/eventResults" => UpdateType.EventResult,
                "/database/bonusResults" => UpdateType.BonusResult,
                "/database/teamElectionStats" => UpdateType.TeamElectionStats,
                "/database/renovationProgress" => UpdateType.RenovationProgress,
                _ => null
            };
    }
}