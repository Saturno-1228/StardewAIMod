using System;
using HarmonyLib;
using StardewModdingAPI;
using StardewValley;

namespace StardewAIMod
{
    public class HarmonyPatches
    {
        private static IMonitor Monitor;
        private static Services.MemoryService Memory;

        public static void Initialize(IMonitor monitor, Services.MemoryService memory)
        {
            Monitor = monitor;
            Memory = memory;
        }

        [HarmonyPatch(typeof(NPC), nameof(NPC.receiveGift))]
        public static class ReceiveGiftPatch
        {
            public static void Postfix(NPC __instance, StardewValley.Object o, Farmer giver)
            {
                if (Memory == null) return;

                try
                {
                    // IDs: Mermaid's Pendant (460), Void Ghost Pendant (808)
                    bool isMermaidsPendant = o.ParentSheetIndex == 460 || o.ItemId == "460";
                    bool isVoidGhostPendant = o.ParentSheetIndex == 808 || o.ItemId == "808";

                    if (isMermaidsPendant || isVoidGhostPendant)
                    {
                        // In 1.6, if the NPC receives it and hasn't rejected it, we assume it's a success
                        // since receiveGift is called. If they accepted it, we log the memory.
                        // Ideally we could check if giver.spouse == __instance.Name, but doing it on receipt is close enough based on instructions.
                        bool asRoommate = isVoidGhostPendant;
                        string engagementMemoryDesc = asRoommate ? "Player asked me to be their roommate, and I accepted!" : "Player proposed to me, and I accepted!";
                        Memory.AddMemory(__instance.Name, engagementMemoryDesc, 5, "love");
                        Monitor.Log($"[Studio Corvus] 💍 Engagement memory injected for {__instance.Name}.", LogLevel.Info);
                        return; // Don't log it as a regular gift
                    }

                    int taste = __instance.getGiftTasteForThisItem(o);
                    int importance = 1;
                    string emotion = "neutral";
                    string tasteDesc = "neutral about";

                    if (taste == NPC.gift_taste_love)
                    {
                        importance = 3;
                        emotion = "love";
                        tasteDesc = "loved";
                    }
                    else if (taste == NPC.gift_taste_like)
                    {
                        importance = 3;
                        emotion = "happy";
                        tasteDesc = "liked";
                    }
                    else if (taste == NPC.gift_taste_dislike)
                    {
                        importance = -3;
                        emotion = "sad";
                        tasteDesc = "disliked";
                    }
                    else if (taste == NPC.gift_taste_hate)
                    {
                        importance = -3;
                        emotion = "angry";
                        tasteDesc = "hated";
                    }

                    string memoryDesc = $"Player gave me {o.DisplayName}. I {tasteDesc} it.";
                    Memory.AddMemory(__instance.Name, memoryDesc, importance, emotion);
                    Monitor.Log($"[Studio Corvus] 🎁 Gift memory injected for {__instance.Name}: {o.DisplayName} ({tasteDesc})", LogLevel.Debug);
                }
                catch (Exception ex)
                {
                    Monitor.Log($"[Studio Corvus] Error injecting gift memory: {ex}", LogLevel.Error);
                }
            }
        }
    }
}
