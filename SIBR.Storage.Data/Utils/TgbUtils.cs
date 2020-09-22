using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Newtonsoft.Json.Linq;
using NodaTime;
using SIBR.Storage.Data.Models;

namespace SIBR.Storage.Data.Utils
{
    public class TgbUtils
    {
        public static Guid GetId(JObject obj)
        {
            if (obj.ContainsKey("_id"))
                return obj["_id"]!.ToObject<Guid>();
            if (obj.ContainsKey("id"))
                return obj["id"]!.ToObject<Guid>();
            throw new ArgumentException("Could not find _id or id key on object");
        }

        public static Guid? TryGetId(JToken token)
        {
            if (!(token is JObject obj))
                return null;

            try
            {
                return (obj["_id"] ?? obj["id"])?.ToObject<Guid>();
            }
            catch (FormatException)
            {
                // invalid guid is null >:3
                return null;
            }
        }

        public static ExtractOutput ExtractUpdatesFromStreamRoot(Guid sourceId, Instant timestamp, JObject root)
        {
            IEnumerable<EntityUpdate> GetUpdates(params (UpdateType type, string path)[] updates)
            {
                return updates.SelectMany(item =>
                    root.SelectTokens(item.path)
                        .Select(token => EntityUpdate.From(item.type, sourceId, timestamp, token)));
            }

            var output = new ExtractOutput();
            output.EntityUpdates.AddRange(GetUpdates(
                (UpdateType.Sim, "value.games.sim"),
                (UpdateType.Standings, "value.games.standings"),
                (UpdateType.Team, "value.leagues.teams.*"),
                (UpdateType.Tiebreakers, "value.leagues.tiebreakers"),
                (UpdateType.Temporal, "value.temporal")
            ).ToList());

            if (root["value"]?["games"]?["schedule"] is JArray schedule)
                output.GameUpdates.AddRange(GameUpdate.FromArray(sourceId, timestamp, schedule));

            return output;
        }

        public class ExtractOutput
        {
            public List<EntityUpdate> EntityUpdates = new List<EntityUpdate>();
            public List<GameUpdate> GameUpdates = new List<GameUpdate>();
        }
    }
}