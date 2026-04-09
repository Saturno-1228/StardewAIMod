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
        /// Guarda todas las memorias en un archivo JSON.
        /// </summary>
        public void SaveAll()
        {
            string path = Path.Combine(_helper.DirectoryPath, "memories.json");
            string json = JsonSerializer.Serialize(_memories, new JsonSerializerOptions
            {
                WriteIndented = true
            });
            File.WriteAllText(path, json);
            _monitor.Log("[Memory] 💾 All memories saved.", StardewModdingAPI.LogLevel.Info);
        }

        /// <summary>
        /// Carga las memorias desde el archivo JSON.
        /// </summary>
        public void LoadAll()
        {
            string path = Path.Combine(_helper.DirectoryPath, "memories.json");
            if (File.Exists(path))
            {
                string json = File.ReadAllText(path);
                _memories = JsonSerializer.Deserialize<Dictionary<string, NpcMemory>>(json)
                            ?? new Dictionary<string, NpcMemory>();
                _monitor.Log($"[Memory] 📂 Loaded memories for {_memories.Count} NPCs.", StardewModdingAPI.LogLevel.Info);
            }
            else
            {
                _memories = new Dictionary<string, NpcMemory>();
                _monitor.Log("[Memory] 📂 No previous memories found. Starting fresh.", StardewModdingAPI.LogLevel.Info);
            }
        }

        private string GetCurrentGameDate()
        {
            try
            {
                return $"{StardewValley.Game1.currentSeason} {StardewValley.Game1.dayOfMonth}, Year {StardewValley.Game1.year}";
            }
            catch
            {
                return "Unknown";
            }
        }
    }
}