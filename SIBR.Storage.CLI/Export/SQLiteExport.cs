using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Serilog;
using SIBR.Storage.CLI.Utils;
using SIBR.Storage.Data;
using SIBR.Storage.Data.Utils;
using SQLite;

namespace SIBR.Storage.CLI.Export
{
    public class SQLiteExport
    {
        private readonly Database _db;
        private readonly PlayerUpdateStore _playerStore;
        private readonly TeamUpdateStore _teamStore;
        private readonly GameStore _gameStore;
        private readonly GameUpdateStore _gameUpdateStore;
        private readonly ILogger _logger;

        public SQLiteExport(Database db, PlayerUpdateStore playerStore, GameStore gameStore, TeamUpdateStore teamStore, ILogger logger, GameUpdateStore gameUpdateStore)
        {
            _db = db;
            _playerStore = playerStore;
            _gameStore = gameStore;
            _teamStore = teamStore;
            _gameUpdateStore = gameUpdateStore;
            _logger = logger.ForContext<SQLiteExport>();
        }

        public async Task Run(Program.ExportDbCmd exportDbCmd)
        {
            var opts = new SQLiteConnectionString(
                "chron.db",
                storeDateTimeAsTicks: false,
                storeTimeSpanAsTicks: false,
                openFlags: SQLiteOpenFlags.ReadWrite | SQLiteOpenFlags.SharedCache | SQLiteOpenFlags.Create);

            using var sqlite = new SQLiteConnection(opts);
            sqlite.Query<object>("pragma journal_mode = wal");
            sqlite.Query<object>("pragma synchronous = normal");

            await SavePlayers(sqlite);
            await SaveTeams(sqlite);
            await SavePlayerUpdates(sqlite);
            await SaveTeamUpdates(sqlite);
            await SaveGames(sqlite);
            await SaveGameUpdates(sqlite);
        }

        private async Task SaveGameUpdates(SQLiteConnection sqlite)
        {
            _logger.Information("Saving game updates...");
            sqlite.CreateTable<SqliteGameUpdate>();
            sqlite.CreateTable<SqliteBaserunner>();

            sqlite.BeginTransaction();
            
            var count = 0;
            await foreach (var buffer in _gameUpdateStore.GetGameUpdates(new GameUpdateStore.GameUpdateQueryOptions(), true).Buffer(1000))
            {
                count += buffer.Count;
                _logger.Information("Saving game updates... {Count} so far", count);
                
                foreach (var game in buffer)
                {
                    var dbGame = new SqliteGameUpdate(game.Data)
                    {
                        game_id = game.GameId.ToString(),
                        hash = game.Hash.ToString(),
                        timestamp = game.Timestamp.ToDateTimeUtc()
                    };
                    sqlite.Insert(dbGame);

                    var occupied = game.Data.Value<int[]>("basesOccupied");
                    var players = game.Data.Value<string[]>("baseRunners");
                    var names = game.Data.Value<string[]>("baseRunnerNames");
                    for (var i = 0; i < occupied.Length; i++)
                        sqlite.Insert(new SqliteBaserunner(game.Hash.ToString(), occupied[i], players[i], names?[i]));
                }
            }
            
            _logger.Information("Committing...");
            sqlite.Commit();
        }

        private async Task SaveGames(SQLiteConnection sqlite)
        {
            _logger.Information("Saving games...");
            sqlite.CreateTable<SqliteGame>();
            sqlite.CreateTable<SqliteOutcomes>();
            sqlite.BeginTransaction();
            
            await foreach (var game in _gameStore.GetGames(new GameStore.GameQueryOptions()))
            {
                var dbGame = new SqliteGame(game.Data)
                {
                    game_id = game.GameId.ToString(),
                    start_time = game.StartTime?.ToDateTimeUtc(),
                    end_time = game.EndTime?.ToDateTimeUtc(),
                };
                sqlite.Insert(dbGame);

                var outcomes = game.Data.Value<string[]>("outcomes");
                for (var i = 0; i < outcomes.Length; i++)
                    sqlite.Insert(new SqliteOutcomes(game.GameId.ToString(), i, outcomes[i]));
            }

            sqlite.Commit();
        }

        private async Task SavePlayerUpdates(SQLiteConnection sqlite)
        {
            _logger.Information("Saving player updates...");
            sqlite.CreateTable<SqlitePlayerUpdate>();
            sqlite.CreateTable<SqliteUpdateAttribute>();
            sqlite.BeginTransaction();
            
            await foreach (var update in _playerStore.GetPlayerVersions(new PlayerUpdateStore.PlayerUpdateQuery()))
            {
                var dbPlayer = new SqlitePlayerUpdate(update.Data)
                {
                    player_id = update.PlayerId.ToString(),
                    update_id = update.UpdateId.ToString(),
                    first_seen = update.FirstSeen.ToDateTimeUtc(),
                    last_seen = update.LastSeen.ToDateTimeUtc(),
                };
                sqlite.Insert(dbPlayer);
                
                InsertUpdateAttributes(sqlite, update.UpdateId.ToString(), update.Data);
            }

            sqlite.Commit();
        }

        private async Task SaveTeamUpdates(SQLiteConnection sqlite)
        {
            _logger.Information("Saving team updates...");
            sqlite.CreateTable<SqliteTeamUpdate>();
            sqlite.CreateTable<SqliteUpdateAttribute>();
            sqlite.BeginTransaction();
            
            await foreach (var update in _teamStore.GetTeamUpdates(new TeamUpdateStore.TeamUpdateQueryOpts()))
            {
                var dbPlayer = new SqliteTeamUpdate(update.Data)
                {
                    team_id = update.TeamId.ToString(),
                    update_id = update.UpdateId.ToString(),
                    first_seen = update.FirstSeen.ToDateTimeUtc(),
                    last_seen = update.LastSeen.ToDateTimeUtc(),
                };
                sqlite.Insert(dbPlayer);
                
                InsertUpdateAttributes(sqlite, update.UpdateId.ToString(), update.Data);
            }

            sqlite.Commit();
        }

        private async Task SaveTeams(SQLiteConnection sqlite)
        {
            _logger.Information("Saving teams...");
            sqlite.CreateTable<SqliteTeam>();
            sqlite.CreateTable<SqliteRosterEntry>();
            sqlite.CreateTable<SqliteAttribute>();
            sqlite.BeginTransaction();

            foreach (var team in await _teamStore.GetTeams())
            {
                var dbTeam = new SqliteTeam(team.Data)
                {
                    team_id = team.TeamId.ToString(),
                    last_update = team.Timestamp.ToDateTimeUtc()
                };
                sqlite.Insert(dbTeam);

                foreach (var position in new[] {"lineup", "rotation", "bullpen", "bench"})
                {
                    var players = team.Data.Value<string[]>(position);
                    for (var i = 0; i < players.Length; i++)
                        sqlite.Insert(new SqliteRosterEntry(team.TeamId.ToString(), players[i], position, i));
                }

                InsertAttributes(sqlite, team.TeamId.ToString(), team.Data);
            }

            sqlite.Commit();
        }
        
        private void InsertAttributes(SQLiteConnection sqlite, string entityId, JsonElement data)
        {
            var perm = data.Value("permAttr", new string[0])
                .Select(attr => new SqliteAttribute(entityId, attr, "permanent"));
            
            var seas = data.Value("seasAttr", new string[0])
                .Select(attr => new SqliteAttribute(entityId, attr, "season"));

            var week = data.Value("weekAttr", new string[0])
                .Select(attr => new SqliteAttribute(entityId, attr, "week"));

            var game = data.Value("gameAttr", new string[0])
                .Select(attr => new SqliteAttribute(entityId, attr, "game"));
            
            foreach (var attr in perm.Concat(seas).Concat(week).Concat(game)) 
                sqlite.Insert(attr);
        }
        
        private void InsertUpdateAttributes(SQLiteConnection sqlite, string updateId, JsonElement data)
        {
            var perm = data.Value("permAttr", new string[0])
                .Select(attr => new SqliteUpdateAttribute(updateId, attr, "permanent"));
            
            var seas = data.Value("seasAttr", new string[0])
                .Select(attr => new SqliteUpdateAttribute(updateId, attr, "season"));

            var week = data.Value("weekAttr", new string[0])
                .Select(attr => new SqliteUpdateAttribute(updateId, attr, "week"));

            var game = data.Value("gameAttr", new string[0])
                .Select(attr => new SqliteUpdateAttribute(updateId, attr, "game"));
            
            foreach (var attr in perm.Concat(seas).Concat(week).Concat(game)) 
                sqlite.Insert(attr);
        }

        private async Task SavePlayers(SQLiteConnection sqlite)
        {
            _logger.Information("Saving players...");
            sqlite.CreateTable<SqlitePlayer>();
            sqlite.CreateTable<SqliteAttribute>();
            sqlite.BeginTransaction();
            
            foreach (var player in await _playerStore.GetAllPlayers())
            {
                var dbPlayer = new SqlitePlayer(player.Data)
                {
                    player_id = player.PlayerId.ToString(),
                    first_seen = player.FirstSeen.ToDateTimeUtc(),
                    last_seen = player.FirstSeen.ToDateTimeUtc(),
                    last_update = player.FirstSeen.ToDateTimeUtc(),
                    current_team = player.TeamId?.ToString(),
                    current_position = player.Position?.ToString()?.ToLowerInvariant(),
                    current_index = player.RosterIndex,
                    is_forbidden = player.IsForbidden
                };
                sqlite.Insert(dbPlayer);
                
                InsertAttributes(sqlite, player.PlayerId.ToString(), player.Data);
            }

            sqlite.Commit();
        }

        public class SqlitePlayerBase
        {
            public string name { get; set; }

            public string bat { get; set; }
            public string armor { get; set; }
            public string ritual { get; set; }
            public int? coffee { get; set; }
            public int? blood { get; set; }

            public double anticapitalism { get; set; }
            public double base_thirst { get; set; }
            public double buoyancy { get; set; }
            public double chasiness { get; set; }
            public double coldness { get; set; }
            public double continuation { get; set; }
            public double? cinnamon { get; set; }
            public double divinity { get; set; }
            public double ground_friction { get; set; }
            public double indulgence { get; set; }
            public double laserlikeness { get; set; }
            public double martyrdom { get; set; }
            public double moxie { get; set; }
            public double musclitude { get; set; }
            public double omniscience { get; set; }
            public double overpowerment { get; set; }
            public double patheticism { get; set; }
            public double pressurization { get; set; }
            public double ruthlessness { get; set; }
            public double shakespearianism { get; set; }
            public double suppression { get; set; }
            public double tenaciousness { get; set; }
            public double thwackability { get; set; }
            public double tragicness { get; set; }
            public double unthwackability { get; set; }
            public double watchfulness { get; set; }

            public double batting_stars { get; set; }
            public double pitching_stars { get; set; }
            public double baserunning_stars { get; set; }
            public double defense_stars { get; set; }

            public bool? peanut_allergy { get; set; }
            public int total_fingers { get; set; }
            public int soul { get; set; }

            // public string perm_attr { get; set; }
            // public string seas_attr { get; set; }
            // public string week_attr { get; set; }
            // public string game_attr { get; set; }

            public SqlitePlayerBase(JsonElement data)
            {
                var stars = PlayerStars.CalculateStars(data);

                name = data.GetProperty("name").GetString();
                
                batting_stars = stars.Batting;
                pitching_stars = stars.Pitching;
                baserunning_stars = stars.Baserunning;
                defense_stars = stars.Defense;

                anticapitalism = data.Value<double>("anticapitalism");
                base_thirst = data.Value<double>("baseThirst");
                buoyancy = data.Value<double>("buoyancy");
                chasiness = data.Value<double>("chasiness");
                coldness = data.Value<double>("coldness");
                continuation = data.Value<double>("continuation");
                cinnamon = data.Value<double?>("cinnamon");
                divinity = data.Value<double>("divinity");
                ground_friction = data.Value<double>("groundFriction");
                indulgence = data.Value<double>("indulgence");
                laserlikeness = data.Value<double>("laserlikeness");
                martyrdom = data.Value<double>("martyrdom");
                moxie = data.Value<double>("moxie");
                musclitude = data.Value<double>("musclitude");
                omniscience = data.Value<double>("omniscience");
                overpowerment = data.Value<double>("overpowerment");
                patheticism = data.Value<double>("patheticism");
                pressurization = data.Value<double>("pressurization");
                ruthlessness = data.Value<double>("ruthlessness");
                shakespearianism = data.Value<double>("shakespearianism");
                suppression = data.Value<double>("suppression");
                tenaciousness = data.Value<double>("tenaciousness");
                thwackability = data.Value<double>("thwackability");
                tragicness = data.Value<double>("tragicness");
                unthwackability = data.Value<double>("unthwackability");
                watchfulness = data.Value<double>("watchfulness");
                
                bat = data.Value<string>("bat").NullIfEmpty();
                armor = data.Value<string>("armor").NullIfEmpty();
                ritual = data.Value<string>("ritual").NullIfEmpty();
                coffee = data.Value<int?>("coffee");
                blood = data.Value<int?>("blood");
                peanut_allergy = data.Value<bool?>("peanutAllergy");
                total_fingers = data.Value<int>("totalFingers");
                soul = data.Value<int>("soul");
                
                // perm_attr = string.Join(";", data.Value("permAttr", new string[0]));
                // seas_attr = string.Join(";", data.Value("seasAttr", new string[0]));
                // week_attr = string.Join(";", data.Value("weekAttr", new string[0]));
                // game_attr = string.Join(";", data.Value("gameAttr", new string[0]));
            }
        }
        
        [SQLite.Table("players")]
        public class SqlitePlayer: SqlitePlayerBase
        {
            [PrimaryKey] public string player_id { get; set; }
            public bool is_forbidden { get; set; }
            public DateTime first_seen { get; set; }
            public DateTime last_seen { get; set; }
            public DateTime last_update { get; set; }
            public string current_team { get; set; }
            public string current_position { get; set; }
            public int? current_index { get; set; }
            
            public SqlitePlayer(JsonElement data) : base(data)
            {
            }
        }

        [SQLite.Table("player_updates")]
        public class SqlitePlayerUpdate: SqlitePlayerBase
        {
            [PrimaryKey] public string update_id { get; set; }
            public string player_id { get; set; }
            public DateTime first_seen { get; set; }
            public DateTime last_seen { get; set; }
            
            public SqlitePlayerUpdate(JsonElement data) : base(data)
            {
            }
        }

        public class SqliteTeamBase
        {
            public string nickname { get; set; }
            public string full_name { get; set; }
            public string location { get; set; }
            public string emoji { get; set; }
            public string slogan { get; set; }
            public string shorthand { get; set; }
            public string main_color { get; set; }
            public string secondary_color { get; set; }
            // public string lineup { get; set; }
            // public string rotation { get; set; }
            // public string bullpen { get; set; }
            // public string bench { get; set; }
            // public string perm_attr { get; set; }
            // public string seas_attr { get; set; }
            // public string week_attr { get; set; }
            // public string game_attr { get; set; }

            public int? shame_runs { get; set; }
            public int total_shames { get; set; }
            public int season_shames { get; set; }
            public int total_shamings { get; set; }
            public int season_shamings { get; set; }
            
            public int rotation_slot { get; set; }
            public int championships { get; set; }

            public SqliteTeamBase(JsonElement data)
            {
                nickname = data.Value<string>("nickname");
                full_name = data.Value<string>("fullName");
                location = data.Value<string>("location");
                emoji = data.Value<string>("emoji").ParseHexEmoji();
                slogan = data.Value<string>("slogan");
                shorthand = data.Value<string>("shorthand");
                main_color = data.Value<string>("mainColor");
                secondary_color = data.Value<string>("secondaryColor");

                // lineup = string.Join(";", data.Value<string[]>("lineup"));
                // rotation = string.Join(";", data.Value<string[]>("rotation"));
                // bullpen = string.Join(";", data.Value<string[]>("bullpen"));
                // bench = string.Join(";", data.Value<string[]>("bench"));

                // perm_attr = string.Join(";", data.Value("permAttr", new string[0]));
                // seas_attr = string.Join(";", data.Value("seasAttr", new string[0]));
                // week_attr = string.Join(";", data.Value("weekAttr", new string[0]));
                // game_attr = string.Join(";", data.Value("gameAttr", new string[0]));

                shame_runs = data.Value<int?>("shameRuns");
                total_shames = data.Value<int>("totalShames");
                season_shames = data.Value<int>("seasonShames");
                total_shamings = data.Value<int>("totalShamings");
                season_shamings = data.Value<int>("seasonShamings");
                rotation_slot = data.Value<int>("rotationSlot");
                championships = data.Value<int>("championships");
            }
        }

        [SQLite.Table("teams")]
        public class SqliteTeam : SqliteTeamBase
        {
            [PrimaryKey] public string team_id { get; set; }
            public DateTime last_update { get; set;}

            public SqliteTeam(JsonElement data) : base(data)
            {
            }
        }
        
        [SQLite.Table("team_updates")]
        public class SqliteTeamUpdate: SqliteTeamBase
        {
            [PrimaryKey] public string update_id { get; set; }
            public string team_id { get; set; }
            public DateTime first_seen { get; set; }
            public DateTime last_seen { get; set; }
            
            public SqliteTeamUpdate(JsonElement data) : base(data)
            {
            }
        }

        public class SqliteGameBase
        {
            public int season { get; set; }
            public int day { get; set; }
            public int phase { get; set; }
            public bool is_postseason { get; set; }
            public bool game_start { get; set; }
            public bool game_complete { get; set; }
            public bool finalized { get; set; }
            // public string outcomes { get; set; }
            public string away_pitcher { get; set; }
            public string away_pitcher_name { get; set; }
            
            public string away_team { get; set; }
            public string away_team_name { get; set; }
            public string away_team_nickname { get; set; }
            public string away_team_color { get; set; }
            public string away_team_secondary_color { get; set; }
            public string away_team_emoji { get; set; }
            public double away_odds { get; set; }
            public int away_strikes { get; set; }
            public int away_score { get; set; }
            public int away_team_batter_count { get; set; }
            public string home_pitcher { get; set; }
            public string home_pitcher_name { get; set; }

            public string home_team { get; set; }
            public string home_team_name { get; set; }
            public string home_team_nickname { get; set; }
            public string home_team_color { get; set; }
            public string home_team_secondary_color { get; set; }
            public string home_team_emoji { get; set; }
            public double home_odds { get; set; }
            public int home_strikes { get; set; }
            public int home_score { get; set; }
            public int home_team_batter_count { get; set; }
            public int inning { get; set; }
            public bool top_of_inning { get; set; }
            public int series_index { get; set; }
            public int series_length { get; set; }
            public bool shame { get; set; }
            public int weather { get; set; }
            public int home_bases { get; set; }
            public int away_bases { get; set; }
            // public string terminology { get; set; }
            // public string rules { get; set; }
            // public string statsheet { get; set; }

            public SqliteGameBase(JsonElement data)
            {
                season = data.Value<int>("season");
                day = data.Value<int>("day");
                phase = data.Value<int>("phase");
                is_postseason = data.Value<bool>("isPostseason");
                game_start = data.Value<bool>("gameStart");
                game_complete = data.Value<bool>("gameComplete");
                finalized = data.Value<bool>("finalized");

                // outcomes = string.Join(";", data.Value<string[]>("outcomes"));
                
                away_pitcher = data.Value<string>("awayPitcher").NullIfEmpty();
                away_pitcher_name = data.Value<string>("awayPitcherName").NullIfEmpty();
                away_team = data.Value<string>("awayTeam");
                away_team_name = data.Value<string>("awayTeamName");
                away_team_nickname = data.Value<string>("awayTeamNickname");
                away_team_color = data.Value<string>("awayTeamColor");
                away_team_secondary_color = data.Value<string>("awayTeamSecondaryColor");
                away_team_emoji = data.Value<string>("awayTeamEmoji").ParseHexEmoji();
                away_odds = data.Value<double>("awayOdds");
                away_strikes = data.Value<int?>("awayStrikes") ?? 3;
                away_score = data.Value<int>("awayScore");
                away_team_batter_count = data.Value<int>("awayTeamBatterCount");
                
                home_pitcher = data.Value<string>("homePitcher").NullIfEmpty();
                home_pitcher_name = data.Value<string>("homePitcherName").NullIfEmpty();
                home_team = data.Value<string>("homeTeam");
                home_team_name = data.Value<string>("homeTeamName");
                home_team_nickname = data.Value<string>("homeTeamNickname");
                home_team_color = data.Value<string>("homeTeamColor");
                home_team_secondary_color = data.Value<string>("homeTeamSecondaryColor");
                home_team_emoji = data.Value<string>("homeTeamEmoji").ParseHexEmoji();
                home_odds = data.Value<double>("homeOdds");
                home_strikes = data.Value<int?>("homeStrikes") ?? 3;
                home_score = data.Value<int>("homeScore");
                home_team_batter_count = data.Value<int>("homeTeamBatterCount");

                inning = data.Value<int>("inning");
                top_of_inning = data.Value<bool>("topOfInning");
                series_index = data.Value<int>("seriesIndex");
                series_length = data.Value<int>("seriesLength");
                shame = data.Value<bool>("shame");
                weather = data.Value<int?>("weather") ?? 1;
                home_bases = data.Value<int?>("homeBases") ?? 4;
                away_bases = data.Value<int?>("awayBases") ?? 4;
                // terminology = data.Value<string>("terminology");
                // rules = data.Value<string>("rules");
                // statsheet = data.Value<string>("statsheet");
            }
        }

        [SQLite.Table("game_updates")]
        public class SqliteGameUpdate : SqliteGameBase
        {
            [PrimaryKey] public string hash { get; set; }
            public string game_id { get; set; }
            public DateTime timestamp { get; set; }
            
            public string away_batter { get; set; }
            public string away_batter_name { get; set; }
            public string home_batter { get; set; }
            public string home_batter_name { get; set; }
            public string last_update { get; set; }
            // public string bases_occupied { get; set; }
            // public string baserunners { get; set; }
            // public string baserunner_names { get; set; }
            public int baserunner_count { get; set; }
            public int half_inning_outs { get; set; }
            public int half_inning_score { get; set; }
            public int at_bat_balls { get; set; }
            public int at_bat_strikes { get; set; }
            public int? repeat_count { get; set; }

            public SqliteGameUpdate(JsonElement data) : base(data)
            {
                last_update = data.Value<string>("lastUpdate");
                // bases_occupied = string.Join(";", data.Value<int[]>("basesOccupied"));
                // baserunners = string.Join(";", data.Value<int[]>("baseRunners"));
                // baserunner_names = string.Join(";", data.Value("baseRunnerNames", new string[0]));
                baserunner_count = data.Value<int>("baserunnerCount");
                half_inning_outs = data.Value<int>("halfInningOuts");
                half_inning_score = data.Value<int>("halfInningScore");
                at_bat_balls = data.Value<int>("atBatBalls");
                at_bat_strikes = data.Value<int>("atBatStrikes");
                repeat_count = data.Value<int?>("repeatCount");
                
                away_batter = data.Value<string>("awayBatter").NullIfEmpty();
                away_batter_name = data.Value<string>("awayBatterName").NullIfEmpty();
                
                home_batter = data.Value<string>("homeBatter").NullIfEmpty();
                home_batter_name = data.Value<string>("homeBatterName").NullIfEmpty();
            }
        }

        [SQLite.Table("games")]
        public class SqliteGame : SqliteGameBase
        {
            [PrimaryKey] public string game_id { get; set; }
            public DateTime? start_time { get; set; }
            public DateTime? end_time { get; set; }

            public SqliteGame(JsonElement data) : base(data)
            {
            }
        }

        [SQLite.Table("rosters")]
        public class SqliteRosterEntry
        {
            public string team_id { get; set; }
            public string player_id { get; set; }
            public string position { get; set; }
            public int roster_index { get; set; }

            public SqliteRosterEntry(string teamId, string playerId, string position, int rosterIndex)
            {
                team_id = teamId;
                player_id = playerId;
                this.position = position;
                roster_index = rosterIndex;
            }
        }
        
        [SQLite.Table("roster_updates")]
        public class SqliteRosterUpdate
        {
            public string team_id { get; set; }
            public string player_id { get; set; }
            public DateTimeOffset first_seen { get; set; }
            public DateTimeOffset last_seen { get; set; }
        }

        [SQLite.Table("outcomes")]
        public class SqliteOutcomes
        {
            public string game_id { get; set; }
            public int outcome_index { get; set; }
            public string text { get; set; }

            public SqliteOutcomes(string gameId, int outcomeIndex, string text)
            {
                game_id = gameId;
                outcome_index = outcomeIndex;
                this.text = text;
            }
        }

        [SQLite.Table("attributes")]
        public class SqliteAttribute
        {
            public string entity_id { get; set; }
            public string attribute_id { get; set; }
            public string duration { get; set; }

            public SqliteAttribute(string entityId, string attributeId, string duration)
            {
                entity_id = entityId;
                attribute_id = attributeId;
                this.duration = duration;
            }
        }
        
        [SQLite.Table("update_attributes")]
        public class SqliteUpdateAttribute
        {
            public string update_id { get; set; }
            public string attribute_id { get; set; }
            public string duration { get; set; }

            public SqliteUpdateAttribute(string updateId, string attributeId, string duration)
            {
                update_id = updateId;
                attribute_id = attributeId;
                this.duration = duration;
            }
        }

        [SQLite.Table("baserunners")]
        public class SqliteBaserunner
        {
            public string hash { get; set; }
            public int position { get; set; }
            public string player_id { get; set; }
            public string player_name { get; set; }

            public SqliteBaserunner(string hash, int position, string playerId, string playerName)
            {
                this.hash = hash;
                this.position = position;
                player_id = playerId;
                player_name = playerName;
            }
        }
    }
}
            