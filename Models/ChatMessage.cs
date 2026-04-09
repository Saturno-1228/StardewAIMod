using System;

namespace StardewAIMod.Models
{
    /// <summary>
    /// Un mensaje individual en la conversación jugador-NPC.
    /// </summary>
    public class ChatMessage
    {
        /// <summary>"player", "npc" o "system".</summary>
        public string Role { get; set; } = "player";

        /// <summary>Contenido del mensaje.</summary>
        public string Content { get; set; } = "";

        /// <summary>Timestamp real (para logs).</summary>
        public DateTime Timestamp { get; set; } = DateTime.Now;

        /// <summary>Fecha dentro del juego.</summary>
        public string GameDate { get; set; } = "";
    }
}