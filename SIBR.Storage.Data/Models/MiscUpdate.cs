using System;
using Newtonsoft.Json.Linq;

namespace SIBR.Storage.Data.Models
{
    public class MiscUpdate: StoredObject
    {
        public const string GlobalEvents = "global_events";
        public const string Idols = "idols";
        public const string OffseasonSetup = "offseason_setup";
        public const string Sim = "sim";
        public const string Temporal = "temporal";
        public const string Tributes = "tributes";
        public const string Tiebreakers = "tiebreakers";
        public const string Standings = "standings";
        public const string Season = "season";
        public const string Postseason = "postseason";

        // todo: store these as arrays or as split-up objects? misc can/should index by ID too
        public const string Leagues = "leagues";
        public const string Subleagues = "subleagues";
        public const string Divisions = "divisions";

        public DateTimeOffset Timestamp;
        public string Type;

        public MiscUpdate(string type, DateTimeOffset timestamp, JToken data) : base(data)
        {
            Timestamp = timestamp;
            Type = type;
        }
    }
}