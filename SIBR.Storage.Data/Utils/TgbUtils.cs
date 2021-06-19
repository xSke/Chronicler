using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Newtonsoft.Json.Linq;
using NodaTime;
using SIBR.Storage.Data.Models;

namespace SIBR.Storage.Data.Utils
{
    public class TgbUtils
    {
        public static Guid GenerateGuidFromString(string key)
        {
            if (Guid.TryParse(key, out var guid))
                return guid;

            var keyBytes = Encoding.UTF8.GetBytes(key);
            return SibrHash.HashAsGuid(keyBytes);
        }
        
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

        public static ExtractOutput ExtractUpdatesFromStreamRoot(Guid sourceId, Instant timestamp, JObject root, SibrHasher hasher, UpdateType? typeFilter = null)
        {
            var output = new ExtractOutput();

            IEnumerable<EntityUpdate> GetUpdates(params (UpdateType type, string path)[] updates)
            {
                return updates
                    .Where(u => typeFilter == null || u.type == typeFilter.Value)
                    .SelectMany(item =>
                        root.SelectTokens(item.path)
                            .Where(token => token.Type != JTokenType.Null && token.First != null)
                            .Select(token => EntityUpdate.From(item.type, sourceId, timestamp, token)));
            }

            output.EntityUpdates.AddRange(GetUpdates(
                (UpdateType.Sim, "value.games.sim"),
                (UpdateType.Season, "value.games.season"),
                (UpdateType.Standings, "value.games.standings"),
                (UpdateType.Tournament, "value.games.tournament"),
                (UpdateType.Team, "value.leagues.teams[*]"),
                (UpdateType.League, "value.leagues.leagues[*]"),
                (UpdateType.Subleague, "value.leagues.subleagues[*]"),
                (UpdateType.Division, "value.leagues.divisions[*]"),
                (UpdateType.Tiebreakers, "value.leagues.tiebreakers[*]"),
                (UpdateType.Temporal, "value.temporal"),
                (UpdateType.Bossfight, "value.fights.bossFights[*]"),
                (UpdateType.Stadium, "value.leagues.stadiums[*]"),
                (UpdateType.CommunityChestProgress, "value.leagues.stats.communityChest"),
                (UpdateType.Playoffs, "value.games.postseasons[*].playoffs"),
                (UpdateType.PlayoffRound, "value.games.postseasons[*].allRounds[*]"),
                (UpdateType.PlayoffMatchup, "value.games.postseasons[*].allMatchups[*]"),
                
                // (from older formats, pre-Underbracket, so we can replay it in)
                (UpdateType.Playoffs, "value.games.postseason.playoffs"),
                (UpdateType.PlayoffRound, "value.games.postseason.round"),
                (UpdateType.PlayoffRound, "value.games.postseason.allRounds[*]"),
                (UpdateType.PlayoffMatchup, "value.games.postseason.matchups[*]"),
                (UpdateType.PlayoffMatchup, "value.games.postseason.allMatchups[*]")
            ));

            if (root["value"]?["games"]?["schedule"] is JArray schedule)
                output.GameUpdates.AddRange(GameUpdate.FromArray(sourceId, timestamp, schedule, hasher));
            // if (root["value"]?["games"]?["tomorrowSchedule"] is JArray tomorrowSchedule)
                // output.GameUpdates.AddRange(GameUpdate.FromArray(sourceId, timestamp, tomorrowSchedule));

            return output;
        }

        public class ExtractOutput
        {
            public List<EntityUpdate> EntityUpdates = new List<EntityUpdate>();
            public List<GameUpdate> GameUpdates = new List<GameUpdate>();
        }
    }
}