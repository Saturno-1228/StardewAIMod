using System;
using System.Collections.Generic;

namespace StardewAIMod.Models
{
    /// <summary>
    /// Representa la memoria de un NPC sobre el jugador y el mundo.
    /// </summary>
    public class NpcMemory
    {
        /// <summary>Nombre interno del NPC (ej: "Abigail", "Shane").</summary>
        public string NpcName { get; set; } = "";

        /// <summary>Nombre que el NPC conoce del jugador.</summary>
        public string PlayerName { get; set; } = "";

        /// <summary>Nivel de amistad actual (0 a 14 corazones).</summary>
        public int FriendshipHearts { get; set; } = 0;

        /// <summary>Estado de la relación (desconocido, amigo, novio/a, esposo/a).</summary>
        public string RelationshipStatus { get; set; } = "Stranger";

        /// <summary>Lista de eventos/acciones que el NPC recuerda.</summary>
        public List<MemoryEntry> Memories { get; set; } = new List<MemoryEntry>();

        /// <summary>Estado emocional actual del NPC.</summary>
        public string CurrentMood { get; set; } = "neutral";

        /// <summary>Último regalo recibido del jugador.</summary>
        public string LastGiftReceived { get; set; } = "";

        /// <summary>¿El NPC vio al jugador hurgar en la basura?</summary>
        public bool SawPlayerDigInTrash { get; set; } = false;

        /// <summary>Notas libres que la IA puede agregar.</summary>
        public string AiNotes { get; set; } = "";

        /// <summary>Historial de la conversación reciente (a corto plazo).</summary>
        public List<ChatMessage> ConversationHistory { get; set; } = new List<ChatMessage>();

        /// <summary>Lista de favores pendientes del NPC hacia el jugador.</summary>
        public List<string> PendingFavors { get; set; } = new List<string>();
    }

    /// <summary>
    /// Una entrada individual de memoria.
    /// </summary>
    public class MemoryEntry
    {
        /// <summary>Qué pasó.</summary>
        public string Description { get; set; } = "";

        /// <summary>Cuándo pasó (día del juego).</summary>
        public string GameDate { get; set; } = "";

        /// <summary>Importancia (1 = trivial, 5 = crucial).</summary>
        public int Importance { get; set; } = 1;

        /// <summary>Emoción asociada.</summary>
        public string Emotion { get; set; } = "neutral";
    }
}