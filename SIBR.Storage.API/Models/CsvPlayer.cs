using System;
using System.Linq;
using Newtonsoft.Json;
using SIBR.Storage.Data.Models;

namespace SIBR.Storage.API.Models
{
    public class CsvPlayer
    {
        public Guid playerId { get; set; }
        public Guid? teamId { get; set; }
        public string position { get; set; }
        public int? rosterIndex { get; set; }
        public bool forbidden { get; set; }
        public string name { get; set; }

        public double anticapitalism { get; set; }
        public double baseThirst { get; set; }
        public double buoyancy { get; set; }
        public double chasiness { get; set; }
        public double coldness { get; set; }
        public double continuation { get; set; }
        public double divinity { get; set; }
        public double groundFriction { get; set; }
        public double indulgence { get; set; }
        public double laserlikeness { get; set; }
        public double martyrdom { get; set; }
        public double moxie { get; set; }
        public double musclitude { get; set; }
        public string bat { get; set; }
        public double omniscience { get; set; }
        public double overpowerment { get; set; }
        public double patheticism { get; set; }
        public double ruthlessness { get; set; }
        public double shakespearianism { get; set; }
        public double suppression { get; set; }
        public double tenaciousness { get; set; }
        public double thwackability { get; set; }
        public double tragicness { get; set; }
        public double unthwackability { get; set; }
        public double watchfulness { get; set; }
        public double pressurization { get; set; }
        public int totalFingers { get; set; }
        public int soul { get; set; }
        public bool deceased { get; set; }
        public bool? peanutAllergy { get; set; }
        public double? cinnamon { get; set; }
        public int? fate { get; set; }
        public string armor { get; set; }
        public string ritual { get; set; }
        public int? coffee { get; set; }
        public int? blood { get; set; }
        [JsonIgnore] public string permAttr { get; set; }
        [JsonIgnore] public string seasAttr { get; set; }
        [JsonIgnore] public string weekAttr { get; set; }
        [JsonIgnore] public string gameAttr { get; set; }
        public double baserunningRating { get; set; }
        public double pitchingRating { get; set; }
        public double hittingRating { get; set; }
        public double defenseRating { get; set; }

        public CsvPlayer(PlayerView player)
        {
            JsonConvert.PopulateObject(player.Data.GetRawText(), this);
            playerId = player.PlayerId;
            teamId = player.TeamId;
            position = player.Position?.ToString()?.ToLowerInvariant();
            rosterIndex = player.RosterIndex;
            forbidden = player.IsForbidden;

            if (player.Data.TryGetProperty("seasAttr", out var seasAttr))
                this.seasAttr = string.Join(";", seasAttr.EnumerateArray().Select(v => v.GetString()));
            
            if (player.Data.TryGetProperty("permAttr", out var permAttr))
                this.permAttr = string.Join(";", permAttr.EnumerateArray().Select(v => v.GetString()));

            if (player.Data.TryGetProperty("gameAttr", out var gameAttr))
                this.gameAttr = string.Join(";", gameAttr.EnumerateArray().Select(v => v.GetString()));
            
            if (player.Data.TryGetProperty("weekAttr", out var weekAttr))
                this.weekAttr = string.Join(";", weekAttr.EnumerateArray().Select(v => v.GetString()));

        }
    }
}