using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;

namespace LivingCompanionsValley.Services
{
    public class ShortTermMemoryManager
    {
        private readonly ConcurrentDictionary<string, Queue<string>> _npcMemories = new ConcurrentDictionary<string, Queue<string>>();
        private const int MaxMemoryLines = 3;

        public void AddInteraction(string npcName, string userText, string npcResponse)
        {
            if (string.IsNullOrWhiteSpace(npcName)) return;

            var queue = _npcMemories.GetOrAdd(npcName, _ => new Queue<string>());
            var interactionLine = $"Granjero: {userText} | {npcName}: {npcResponse}";

            lock (queue)
            {
                queue.Enqueue(interactionLine);
                while (queue.Count > MaxMemoryLines)
                {
                    queue.Dequeue();
                }
            }
        }

        public string GetRecentContext(string npcName)
        {
            if (string.IsNullOrWhiteSpace(npcName)) return string.Empty;

            if (_npcMemories.TryGetValue(npcName, out var queue))
            {
                lock (queue)
                {
                    if (queue.Count == 0) return string.Empty;
                    return string.Join("\n", queue);
                }
            }

            return string.Empty;
        }
    }
}
