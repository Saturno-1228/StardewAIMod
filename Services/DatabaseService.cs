using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using LiteDB;
using LivingCompanionsValley.Models;

namespace LivingCompanionsValley.Services
{
    public class DatabaseService : IDisposable
    {
        private readonly LiteDatabase _db;
        private readonly ILiteCollection<LoreChunk> _loreChunks;

        public DatabaseService(string modDirectory)
        {
            string dataPath = Path.Combine(modDirectory, "Data");
            if (!Directory.Exists(dataPath))
            {
                Directory.CreateDirectory(dataPath);
            }

            string dbPath = Path.Combine(dataPath, "LoreDatabase.db");
            _db = new LiteDatabase(dbPath);
            _loreChunks = _db.GetCollection<LoreChunk>("lore_chunks");

            // Ensure index for faster querying
            _loreChunks.EnsureIndex(x => x.NpcName);
            _loreChunks.EnsureIndex(x => x.Category);
        }

        public void SyncLoreCategory(string npcName, string category, List<string> chunks)
        {
            // Delete existing records matching the npcName and category
            _loreChunks.DeleteMany(x => x.NpcName == npcName && x.Category == category);

            // Insert new chunks
            if (chunks != null && chunks.Count > 0)
            {
                var newChunks = chunks.Select(text => new LoreChunk
                {
                    Id = ObjectId.NewObjectId(),
                    NpcName = npcName,
                    Category = category,
                    TextContent = text
                }).ToList();

                _loreChunks.InsertBulk(newChunks);
            }
        }

        public List<string> RetrieveRelevantLore(string npcName, string userQuery)
        {
            // Basic retrieval: return all text from this NPC for now
            return _loreChunks
                .Find(x => x.NpcName == npcName)
                .Select(x => x.TextContent)
                .ToList();
        }

        public void Dispose()
        {
            _db?.Dispose();
        }
    }
}
