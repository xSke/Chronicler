#nullable enable
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
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

        protected override async Task ProcessFile(string filename, IAsyncEnumerable<JToken> entries)
        {
            await using var conn = await _db.Obtain();
            
            var updates = new List<EntityUpdate>();
            var gameUpdates = new List<GameUpdate>();
            
            _logger.Information("Parsing updates...");
            await foreach (var entry in entries)
            {
                var data = entry["data"]!;

                if (data is JObject {Count: 0})
                    // skip empty objects, we get those sometimes
                    continue;

                var endpoint = entry.Value<string>("endpoint");
                var timestamp = Instant.FromUnixTimeMilliseconds((long) (entry.Value<double>("time") * 1000));

                var idString = entry["id"]!.ToObject<string?>();
                var id = idString != null ? TgbUtils.GenerateGuidFromString(idString) : (Guid?) null;

                if (GetSimpleUpdateType(endpoint) is UpdateType simpleType)
                {
                    updates.Add(EntityUpdate.From(simpleType, _sourceId, timestamp, data, _hasher, id));
                }
                else if (endpoint == "/events/streamData")
                {
                    updates.Add(EntityUpdate.From(UpdateType.Stream, _sourceId, timestamp, data, _hasher));
                    
                    var extract = TgbUtils.ExtractUpdatesFromStreamRoot(_sourceId, timestamp, (JObject) data, _hasher);
                    updates.AddRange(extract.EntityUpdates);
                    gameUpdates.AddRange(extract.GameUpdates);
                }
            }
            
            _logger.Information("Parsed {UpdateCount} updates and {GameUpdateCount} game updates, saving...",
                updates.Count, gameUpdates.Count);

            await using (var tx = await conn.BeginTransactionAsync())
            {
                await _updateStore.SaveUpdates(conn, updates, false);
                await _gameUpdateStore.SaveGameUpdates(conn, gameUpdates, false, false);
                await tx.CommitAsync();
            }
            
            await _gameUpdateStore.UpdateSearchIndex(conn);
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
                _ => null
            };
    }
}