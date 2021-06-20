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
                UpdateType.Stream,
                UpdateType.Idols,
                UpdateType.Tributes,
                UpdateType.Temporal,
                UpdateType.Tiebreakers,
                UpdateType.Sim,
                UpdateType.GlobalEvents,
                UpdateType.OffseasonSetup,
                UpdateType.Standings,
                UpdateType.Season,
                UpdateType.League,
                UpdateType.Subleague,
                UpdateType.Division,
                UpdateType.GameStatsheet,
                UpdateType.TeamStatsheet,
                UpdateType.PlayerStatsheet,
                UpdateType.SeasonStatsheet,
                UpdateType.Bossfight,
                UpdateType.OffseasonRecap,
                UpdateType.BonusResult,
                UpdateType.DecreeResult,
                UpdateType.EventResult,
                UpdateType.Playoffs,
                UpdateType.PlayoffRound,
                UpdateType.PlayoffMatchup,
                UpdateType.Tournament,
                UpdateType.Stadium,
                UpdateType.RenovationProgress,
                UpdateType.TeamElectionStats,
                UpdateType.Item,
                UpdateType.CommunityChestProgress,
                UpdateType.GiftProgress,
                UpdateType.ShopSetup,
                UpdateType.SunSun,
                UpdateType.LibraryStory
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