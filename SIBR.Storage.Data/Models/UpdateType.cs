namespace SIBR.Storage.Data.Models
{
    public enum UpdateType: byte
    {
        Player = 1,
        Team = 2,
        Stream = 3,
        Game = 4,
        Idols = 5,
        Tributes = 6,
        Temporal = 7,
        Tiebreakers = 8,
        Sim = 9,
        GlobalEvents = 10,
        OffseasonSetup = 11,
        Standings = 12,
        Season = 13,
        League = 15,
        Subleague = 16,
        Division = 17,
        GameStatsheet = 18,
        TeamStatsheet = 19,
        PlayerStatsheet = 20,
        SeasonStatsheet = 21,
        Bossfight = 22,
        OffseasonRecap = 23,
        BonusResult = 24,
        DecreeResult = 25,
        EventResult = 26,
        Playoffs = 27,
        PlayoffRound = 28,
        PlayoffMatchup = 29
    }
}