using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json.Linq;
using Npgsql;
using SIBR.Storage.Data;
using SIBR.Storage.Data.Models;

namespace SIBR.Storage.Ingest
{
    public class StreamDataWorker : BaseWorker
    {
        private readonly EventStream _eventStream;
        private readonly Database _db;
        private readonly StreamUpdateStore _streamStore;
        private readonly GameUpdateStore _gameStore;
        private readonly TeamUpdateStore _teamStore;
        private readonly MiscStore _miscStore;

        public StreamDataWorker(IServiceProvider services) : base(services)
        {
            _eventStream = services.GetRequiredService<EventStream>();
            _streamStore = services.GetRequiredService<StreamUpdateStore>();
            _db = services.GetRequiredService<Database>();
            _gameStore = services.GetRequiredService<GameUpdateStore>();
            _teamStore = services.GetRequiredService<TeamUpdateStore>();
            _miscStore = services.GetRequiredService<MiscStore>();
        }

        private async Task HandleStreamData(string obj)
        {
            var timestamp = DateTimeOffset.UtcNow;
            var data = JObject.Parse(obj);

            await using var conn = await _db.Obtain();
            await using var tx = await conn.BeginTransactionAsync();

            await _streamStore.SaveUpdates(conn, new[] {new StreamUpdate(timestamp, data)});

            var gamesRes = new UpdateStoreResult();
            if (data["value"]?["games"]?["schedule"] is JArray scheduleObj)
                gamesRes = await SaveGameUpdates(conn, timestamp, scheduleObj.OfType<JObject>());

            var teamsRes = new UpdateStoreResult();
            if (data["value"]?["leagues"]?["teams"] is JArray teamsObj)
                teamsRes = await SaveTeamUpdates(conn, timestamp, teamsObj.OfType<JObject>());

            var miscRes = await _miscStore.SaveMiscUpdates(conn, ExtractMiscObjects(data, timestamp));

            _logger.Information(
                "Saved {GameUpdates} game updates ({GameObjects} new), {TeamUpdates} team updates ({TeamObjects} new), {MiscUpdates} misc updates ({MiscObjects} new)",
                gamesRes.NewUpdates, gamesRes.NewObjects, teamsRes.NewUpdates, teamsRes.NewObjects, miscRes.NewUpdates,
                miscRes.NewObjects);
            
            await tx.CommitAsync();
        }

        private List<MiscUpdate> ExtractMiscObjects(JObject data, DateTimeOffset timestamp)
        {
            var misc = new List<MiscUpdate>();

            void TryAdd(string type, string path)
            {
                var token = data.SelectToken(path);
                if (token != null)
                    misc.Add(new MiscUpdate(type, timestamp, token));
            }

            TryAdd(MiscUpdate.Sim, "value.games.sim");
            TryAdd(MiscUpdate.Standings, "value.games.standings");
            TryAdd(MiscUpdate.Season, "value.games.season");
            TryAdd(MiscUpdate.Postseason, "value.games.postseason");
            TryAdd(MiscUpdate.Leagues, "value.leagues.leagues");
            TryAdd(MiscUpdate.Subleagues, "value.leagues.subleagues");
            TryAdd(MiscUpdate.Divisions, "value.leagues.divisions");
            TryAdd(MiscUpdate.Tiebreakers, "value.leagues.tiebreakers");
            TryAdd(MiscUpdate.Temporal, "value.temporal");

            return misc;
        }

        private async Task<UpdateStoreResult> SaveTeamUpdates(NpgsqlConnection conn, DateTimeOffset timestamp,
            IEnumerable<JObject> teams) =>
            await _teamStore.SaveTeamUpdates(conn, teams.Select(team => new TeamUpdate(timestamp, team)).ToList());

        private async Task<UpdateStoreResult> SaveGameUpdates(NpgsqlConnection conn, DateTimeOffset timestamp,
            IEnumerable<JObject> gameUpdates) =>
            await _gameStore.SaveGameUpdates(conn,
                gameUpdates.Select(update => new GameUpdate(timestamp, update)).ToArray());

        protected override async Task Run()
        {
            _logger.Information("Starting stream data consumer");
            await _eventStream.OpenStream("https://www.blaseball.com/events/streamData", async (data) =>
            {
                try
                {
                    await HandleStreamData(data);
                }
                catch (Exception e)
                {
                    _logger.Error(e, "Error while processing stream data");
                }
            });
        }
    }
}