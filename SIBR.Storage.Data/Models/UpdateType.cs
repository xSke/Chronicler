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
        Standings = 12
    }
}