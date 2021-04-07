using System.Threading.Tasks;
using SIBR.Storage.Data;
using SIBR.Storage.Data.Models;

namespace SIBR.Storage.CLI
{
    public class VersionRebuild
    {
        private readonly Database _db;
        private readonly VersionStore _store;

        public VersionRebuild(Database db, VersionStore store)
        {
            _db = db;
            _store = store;
        }

        public async Task FullRebuild()
        {
            await using var conn = await _db.Obtain();

            var types = new[]
            {
                UpdateType.Team,
                UpdateType.Player,
                UpdateType.Temporal,
                UpdateType.Idols,
                UpdateType.Tributes,
                UpdateType.Tiebreakers,
                UpdateType.Sim,
                UpdateType.GlobalEvents,
                UpdateType.OffseasonSetup,
                UpdateType.OffseasonRecap,
                UpdateType.Standings,
                UpdateType.Season,
                UpdateType.Bossfight,
                UpdateType.BonusResult,
                UpdateType.DecreeResult,
                UpdateType.EventResult,
                UpdateType.Tournament,
                UpdateType.SeasonStatsheet,
                UpdateType.GameStatsheet,
                UpdateType.PlayerStatsheet,
                UpdateType.TeamStatsheet,
                UpdateType.Stadium,
                UpdateType.RenovationProgress,
                UpdateType.TeamElectionStats,
                UpdateType.Stream
            };

            foreach (var type in types)
            {
                await using var tx = await conn.BeginTransactionAsync();
                await _store.RebuildAll(conn, type);
                await tx.CommitAsync();
            }
        }
    }
}