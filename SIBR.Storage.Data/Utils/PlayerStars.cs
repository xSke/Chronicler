using System;
using System.Text.Json;

namespace SIBR.Storage.Data.Utils
{
    public class PlayerStars
    {
        public double Batting { get; }
        public double Pitching { get; }
        public double Baserunning { get; }
        public double Defense { get; }

        public PlayerStars(double batting, double pitching, double baserunning, double defense)
        {
            Batting = batting;
            Pitching = pitching;
            Baserunning = baserunning;
            Defense = defense;
        }

        public static PlayerStars CalculateStars(JsonElement playerData)
        {
            return new PlayerStars(
                CalculateBattingSkill(
                    playerData.GetProperty("tragicness").GetDouble(),
                    playerData.GetProperty("buoyancy").GetDouble(),
                    playerData.GetProperty("thwackability").GetDouble(),
                    playerData.GetProperty("moxie").GetDouble(),
                    playerData.GetProperty("divinity").GetDouble(),
                    playerData.GetProperty("musclitude").GetDouble(),
                    playerData.GetProperty("patheticism").GetDouble(),
                    playerData.GetProperty("martyrdom").GetDouble()
                ) * 5,
                CalculatePitchingSkill(
                    playerData.GetProperty("shakespearianism").GetDouble(),
                    playerData.GetProperty("suppression").GetDouble(),
                    playerData.GetProperty("unthwackability").GetDouble(),
                    playerData.GetProperty("coldness").GetDouble(),
                    playerData.GetProperty("overpowerment").GetDouble(),
                    playerData.GetProperty("ruthlessness").GetDouble()
                ) * 5,
                CalculateBaserunningSkill(
                    playerData.GetProperty("laserlikeness").GetDouble(),
                    playerData.GetProperty("continuation").GetDouble(),
                    playerData.GetProperty("baseThirst").GetDouble(),
                    playerData.GetProperty("indulgence").GetDouble(),
                    playerData.GetProperty("groundFriction").GetDouble()
                ) * 5,
                CalculateDefenseSkill(
                    playerData.GetProperty("omniscience").GetDouble(),
                    playerData.GetProperty("tenaciousness").GetDouble(),
                    playerData.GetProperty("watchfulness").GetDouble(),
                    playerData.GetProperty("anticapitalism").GetDouble(),
                    playerData.GetProperty("chasiness").GetDouble()
                ) * 5
            );
        }

        public static double CalculateBattingSkill(
            double tragicness,
            double buoyancy,
            double thwackability,
            double moxie,
            double divinity,
            double musclitude,
            double patheticisim,
            double martyrdom)
        {
            return Math.Pow(1 - tragicness, 0.01) *
                   Math.Pow(buoyancy, 0) *
                   Math.Pow(thwackability, 0.35) *
                   Math.Pow(moxie, 0.075) *
                   Math.Pow(divinity, 0.35) *
                   Math.Pow(musclitude, 0.075) *
                   Math.Pow(1 - patheticisim, 0.05) *
                   Math.Pow(martyrdom, 0.02);
        }

        public static double CalculatePitchingSkill(
            double shakespearianism,
            double suppression,
            double unthwackability,
            double coldness,
            double overpowerment,
            double ruthlessness)
        {
            return Math.Pow(shakespearianism, 0.1) *
                   Math.Pow(suppression, 0) *
                   Math.Pow(unthwackability, 0.5) *
                   Math.Pow(coldness, 0.025) *
                   Math.Pow(overpowerment, 0.15) *
                   Math.Pow(ruthlessness, 0.4);
        }

        public static double CalculateBaserunningSkill(
            double laserlikeness,
            double continuation,
            double baseThirst,
            double indulgence,
            double groundFriction)
        {
            return Math.Pow(laserlikeness, 0.5) *
                   Math.Pow(continuation, 0.1) *
                   Math.Pow(baseThirst, 0.1) *
                   Math.Pow(indulgence, 0.1) *
                   Math.Pow(groundFriction, 0.1);
        }

        public static double CalculateDefenseSkill(
            double omniscience,
            double tenaciousness,
            double watchfulness,
            double anticapitalism,
            double chasiness)
        {
            return Math.Pow(omniscience, 0.2) *
                   Math.Pow(tenaciousness, 0.2) *
                   Math.Pow(watchfulness, 0.2) *
                   Math.Pow(anticapitalism, 0.1) *
                   Math.Pow(chasiness, 0.1);
        }
    }
}