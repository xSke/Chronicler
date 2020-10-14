using System;
using System.Linq;
using Newtonsoft.Json;
using SIBR.Storage.Data.Models;

namespace SIBR.Storage.API.Models
{
    public class CsvGame
    {
        public Guid gameId { get; set; }
        public DateTimeOffset? startTime { get; set; }
        public DateTimeOffset? endTime { get; set; }
        
        public int season { get; set; }
        public int day { get; set; }
        
        public bool gameStart { get; set; }
        public bool gameComplete { get; set; }

        public int inning { get; set; }
        public bool topOfInning { get; set; }

        public int seriesIndex { get; set; }
        public int seriesLength { get; set; }
        public bool isPostseason { get; set; }
        public int? weather { get; set; }
        public bool shame { get; set; }

        [JsonIgnore] public string outcomes { get; set; }
        public Guid? awayPitcher { get; set; }
        public string awayPitcherName { get; set; }
        public Guid awayTeam { get; set; }
        public string awayTeamName { get; set; }
        public string awayTeamNickname { get; set; }
        public string awayTeamColor { get; set; }
        public string awayTeamSecondaryColor { get; set; }
        public string awayTeamEmoji { get; set; }
        public double awayOdds { get; set; }
        public int? awayStrikes { get; set; }
        public int? awayBases { get; set; }
        public double awayScore { get; set; }
        public int awayTeamBatterCount { get; set; }
        
        public Guid? homePitcher { get; set; }
        public string homePitcherName { get; set; }
        public Guid homeTeam { get; set; }
        public string homeTeamName { get; set; }
        public string homeTeamNickname { get; set; }
        public string homeTeamColor { get; set; }
        public string homeTeamSecondaryColor { get; set; }
        public string homeTeamEmoji { get; set; }
        public double homeOdds { get; set; }
        public int? homeStrikes { get; set; }
        public int? homeBases { get; set; }
        public double homeScore { get; set; }
        public int homeTeamBatterCount { get; set; }

        public CsvGame(GameView game)
        {
            JsonConvert.PopulateObject(game.Data.GetRawText(), this);
            gameId = game.GameId;
            startTime = game.StartTime?.ToDateTimeOffset();
            endTime = game.EndTime?.ToDateTimeOffset();

            if (game.Data.TryGetProperty("outcomes", out var outcomes))
                this.outcomes = string.Join(";", outcomes.EnumerateArray().Select(v => v.GetString()));
        }
    }
}