using System;
using StardewValley;
using StardewValley.ItemTypeDefinitions;

namespace LivingCompanionsValley.Services
{
    public readonly struct EnvironmentSnapshot
    {
        public int TimeOfDay { get; }
        public string Season { get; }
        public bool IsRaining { get; }
        public float PlayerHealthPercent { get; }
        public string HeldItemName { get; }

        public EnvironmentSnapshot(int timeOfDay, string season, bool isRaining, float playerHealthPercent, string heldItemName)
        {
            TimeOfDay = timeOfDay;
            Season = season;
            IsRaining = isRaining;
            PlayerHealthPercent = playerHealthPercent;
            HeldItemName = heldItemName;
        }
    }

    public class EnvironmentObserver
    {
        public EnvironmentSnapshot CaptureSnapshot(Farmer player, GameLocation location)
        {
            int timeOfDay = Game1.timeOfDay;
            string season = Game1.currentSeason ?? "spring";
            bool isRaining = Game1.isRaining;

            float healthPercent = (float)player.health / Math.Max(1, player.maxHealth);

            string heldItemName = "nada";
            if (player.CurrentItem != null)
            {
                string displayName = ItemRegistry.GetData(player.CurrentItem.QualifiedItemId)?.DisplayName;
                if (!string.IsNullOrEmpty(displayName))
                {
                    heldItemName = displayName;
                }
            }

            return new EnvironmentSnapshot(timeOfDay, season, isRaining, healthPercent, heldItemName);
        }

        public string BuildPromptContext(EnvironmentSnapshot snapshot)
        {
            int hours = snapshot.TimeOfDay / 100;
            int minutes = snapshot.TimeOfDay % 100;
            int displayHours = hours >= 24 ? hours - 24 : hours;

            string timePeriod = snapshot.TimeOfDay < 1200 ? "por la mañana" : (snapshot.TimeOfDay < 1900 ? "por la tarde" : "de noche");
            string timeString = $"Es {timePeriod} ({displayHours:D2}:{minutes:D2}).";

            string season = snapshot.Season.ToLower() switch
            {
                "spring" => "Primavera",
                "summer" => "Verano",
                "fall" => "Otoño",
                "winter" => "Invierno",
                _ => snapshot.Season
            };

            string weather = snapshot.IsRaining ? "Está lloviendo." : "El clima está despejado.";

            string health = "parece tener buena salud";
            if (snapshot.PlayerHealthPercent < 0.30f)
            {
                health = "está gravemente herido, exhausto y a punto de desmayarse";
            }
            else if (snapshot.PlayerHealthPercent < 0.75f)
            {
                health = "tiene desgaste visible y algunos rasguños";
            }

            string item = snapshot.HeldItemName == "nada"
                ? "tiene las manos vacías"
                : $"sostiene un/a [{snapshot.HeldItemName}] en sus manos";

            return $"Contexto visual: Es {season}. {weather} {timeString} El granjero {health} y {item}.";
        }
    }
}
