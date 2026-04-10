using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using StardewAIMod.Models;
using StardewModdingAPI;

namespace StardewAIMod.Services
{
    /// <summary>
    /// Gestiona las memorias de todos los NPCs.
    /// Guarda y carga desde archivos JSON por partida.
    /// </summary>
    public class MemoryService
    {
        private readonly IModHelper _helper;
        private readonly IMonitor _monitor;
        private readonly int _maxMemoryPerNpc;

        // Diccionario: NpcName -> NpcMemory
        private Dictionary<string, NpcMemory> _memories;

        public MemoryService(IModHelper helper, IMonitor monitor, int maxMemoryPerNpc)
        {
            _helper = helper;
            _monitor = monitor;
            _maxMemoryPerNpc = maxMemoryPerNpc;
            _memories = new Dictionary<string, NpcMemory>();
        }

        /// <summary>
        /// Obtiene la memoria de un NPC. Si no existe, la crea vacía.
        /// </summary>
        public NpcMemory GetMemory(string npcName)
        {
            if (!_memories.ContainsKey(npcName))
            {
                _memories[npcName] = new NpcMemory { NpcName = npcName };
            }
            return _memories[npcName];
        }

        /// <summary>
        /// Agrega un mensaje al historial reciente de la conversación.
        /// Retiene solo los últimos mensajes para mantener el contexto.
        /// </summary>
        public void AddToConversationHistory(string npcName, string role, string content)
        {
            var memory = GetMemory(npcName);
            memory.ConversationHistory.Add(new StardewAIMod.Models.ChatMessage { Role = role, Content = content });

            // Mantener solo los últimos 20 mensajes de la conversación
            if (memory.ConversationHistory.Count > 20)
            {
                // Dejamos los últimos 20 (tomar al final)
                memory.ConversationHistory = memory.ConversationHistory
                    .Skip(memory.ConversationHistory.Count - 20)
                    .ToList();
            }
        }

        /// <summary>
        /// Agrega un recuerdo a un NPC.
        /// Si supera el máximo, elimina los menos importantes.
        /// </summary>
        public void AddMemory(string npcName, string description, int importance, string emotion)
        {
            var memory = GetMemory(npcName);
            memory.Memories.Add(new MemoryEntry
            {
                Description = description,
                GameDate = GetCurrentGameDate(),
                Importance = importance,
                Emotion = emotion
            });

            // Recortar si supera el máximo
            if (memory.Memories.Count > _maxMemoryPerNpc)
            {
                // Ordenar por importancia desc, luego quedarse con los últimos
                memory.Memories = memory.Memories
                    .OrderByDescending(m => m.Importance)
                    .Take(_maxMemoryPerNpc)
                    .ToList();
            }

            _monitor.Log($"[Memory] {npcName} recorded: {description}", StardewModdingAPI.LogLevel.Debug);
        }

        /// <summary>
        /// Guarda todas las memorias en un archivo JSON usando la API segura de SMAPI.
        /// </summary>
        public void SaveAll()
        {
            try
            {
                _helper.Data.WriteJsonFile("memories.json", _memories);
                _monitor.Log("[Memory] 💾 All memories saved.", StardewModdingAPI.LogLevel.Info);
            }
            catch (Exception ex)
            {
                _monitor.Log($"[Memory] ❌ Error saving memories: {ex}", StardewModdingAPI.LogLevel.Error);
            }
        }

        /// <summary>
        /// Carga las memorias desde el archivo JSON usando la API segura de SMAPI.
        /// </summary>
        public void LoadAll()
        {
            try
            {
                _memories = _helper.Data.ReadJsonFile<Dictionary<string, NpcMemory>>("memories.json")
                            ?? new Dictionary<string, NpcMemory>();

                if (_memories.Count > 0)
                {
                    _monitor.Log($"[Memory] 📂 Loaded memories for {_memories.Count} NPCs.", StardewModdingAPI.LogLevel.Info);
                }
                else
                {
                    _monitor.Log("[Memory] 📂 No previous memories found. Starting fresh.", StardewModdingAPI.LogLevel.Info);
                }
            }
            catch (Exception ex)
            {
                _monitor.Log($"[Memory] ❌ Error loading memories, starting fresh. {ex}", StardewModdingAPI.LogLevel.Error);
                _memories = new Dictionary<string, NpcMemory>();
            }
        }

        private string GetCurrentGameDate()
        {
            try
            {
                return $"{StardewValley.Game1.season} {StardewValley.Game1.dayOfMonth}, Year {StardewValley.Game1.year}";
            }
            catch
            {
                return "Unknown";
            }
        }

        public IEnumerable<string> GetAllNpcsWithMemories()
        {
            return _memories.Keys;
        }

        public List<string> GetPendingFavorsAndClear(string npcName)
        {
            var memory = GetMemory(npcName);
            var copy = memory.PendingFavors.ToList();
            memory.PendingFavors.Clear();
            return copy;
        }
    }
}