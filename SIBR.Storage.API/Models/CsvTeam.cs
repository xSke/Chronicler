using System;
using System.Linq;
using Newtonsoft.Json;
using SIBR.Storage.Data.Models;

namespace SIBR.Storage.API.Models
{
    public class CsvTeam
    {
        public Guid teamId { get; set; }
        
        public string fullName { get; set; }
        public string nickname { get; set; }
        public string emoji { get; set; }
        public string location { get; set; }
        public string mainColor { get; set; }
        public string secondaryColor { get; set; }
        public string shorthand { get; set; }
        public string slogan { get; set; }

        public int totalShames { get; set; }
        public int seasonShames { get; set; }
        public int totalShamings { get; set; }
        public int seasonShamings { get; set; }
        public int shameRuns { get; set; }
        public int championships { get; set; }
        public int rotationSlot { get; set; }
        
        [JsonIgnore] public string lineup { get; set; }
        [JsonIgnore] public string rotation { get; set; }
        [JsonIgnore] public string bullpen { get; set; }
        [JsonIgnore] public string bench { get; set; }
        
        [JsonIgnore] public string permAttr { get; set; }
        [JsonIgnore] public string seasAttr { get; set; }
        [JsonIgnore] public string weekAttr { get; set; }
        [JsonIgnore] public string gameAttr { get; set; }

        public CsvTeam(TeamView team)
        {
            JsonConvert.PopulateObject(team.Data.GetRawText(), this);
            teamId = team.TeamId;

            if (team.Data.TryGetProperty("seasAttr", out var seasAttr))
                this.seasAttr = string.Join(";", seasAttr.EnumerateArray().Select(v => v.GetString()));
            
            if (team.Data.TryGetProperty("permAttr", out var permAttr))
                this.permAttr = string.Join(";", permAttr.EnumerateArray().Select(v => v.GetString()));

            if (team.Data.TryGetProperty("gameAttr", out var gameAttr))
                this.gameAttr = string.Join(";", gameAttr.EnumerateArray().Select(v => v.GetString()));
            
            if (team.Data.TryGetProperty("weekAttr", out var weekAttr))
                this.weekAttr = string.Join(";", weekAttr.EnumerateArray().Select(v => v.GetString()));
            
            
            if (team.Data.TryGetProperty("lineup", out var lineup))
                this.lineup = string.Join(";", lineup.EnumerateArray().Select(v => v.GetString()));

            if (team.Data.TryGetProperty("rotation", out var rotation))
                this.rotation = string.Join(";", rotation.EnumerateArray().Select(v => v.GetString()));

            if (team.Data.TryGetProperty("bullpen", out var bullpen))
                this.bullpen = string.Join(";", bullpen.EnumerateArray().Select(v => v.GetString()));

            if (team.Data.TryGetProperty("bench", out var bench))
                this.bench = string.Join(";", bench.EnumerateArray().Select(v => v.GetString()));
        }
    }
}