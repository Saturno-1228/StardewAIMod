using System.Collections.Generic;
using System.Text;
using StardewAIMod.Models;

namespace StardewAIMod.Services
{
    /// <summary>
    /// Construye los system prompts para cada NPC.
    /// Combina: personalidad base + memorias + contexto del momento.
    /// </summary>
    public class PromptBuilder
    {
        /// <summary>
        /// Genera el system prompt completo para un NPC.
        /// </summary>
        /// <param name="npcName">Nombre del NPC.</param>
        /// <param name="memory">Memorias del NPC.</param>
        /// <param name="currentContext">Contexto actual (clima, hora, ubicación).</param>
        /// <returns>System prompt listo para Venice API.</returns>
        public string BuildSystemPrompt(
            string npcName,
            NpcMemory memory,
            Dictionary<string, string> currentContext)
        {
            var sb = new StringBuilder();

            // ── IDENTIDAD BASE ──
            sb.AppendLine($"You are {npcName} from Stardew Valley.");
            sb.AppendLine($"Stay in character at all times. Never break character.");
            sb.AppendLine($"Never mention you are an AI, a language model, or a program.");
            sb.AppendLine();

            // ── PERSONALIDAD (placeholder - se llenará por NPC) ──
            sb.AppendLine($"[PERSONALITY]");
            sb.AppendLine(GetBasePersonality(npcName));
            sb.AppendLine();

            // ── RELACIÓN CON EL JUGADOR ──
            sb.AppendLine($"[RELATIONSHIP WITH PLAYER]");
            sb.AppendLine($"Player name: {memory.PlayerName}");
            sb.AppendLine($"Friendship level: {memory.FriendshipHearts} hearts out of 14");
            sb.AppendLine($"Relationship status: {memory.RelationshipStatus}");
            sb.AppendLine($"Your current mood: {memory.CurrentMood}");

            if (!string.IsNullOrEmpty(memory.LastGiftReceived))
                sb.AppendLine($"Last gift from player: {memory.LastGiftReceived}");

            if (memory.SawPlayerDigInTrash)
                sb.AppendLine("You SAW the player digging through trash. React accordingly.");

            sb.AppendLine();

            // ── MEMORIAS ──
            if (memory.Memories.Count > 0)
            {
                sb.AppendLine("[MEMORIES - Things you remember about this player]");
                foreach (var mem in memory.Memories)
                {
                    sb.AppendLine($"- ({mem.GameDate}) {mem.Description} [felt: {mem.Emotion}]");
                }
                sb.AppendLine();
            }

            // ── CONTEXTO ACTUAL ──
            if (currentContext != null && currentContext.Count > 0)
            {
                sb.AppendLine("[CURRENT CONTEXT]");
                sb.AppendLine($"- Game Language Code: {StardewValley.LocalizedContentManager.CurrentLanguageCode.ToString()}");
                foreach (var kvp in currentContext)
                {
                    sb.AppendLine($"- {kvp.Key}: {kvp.Value}");
                }
                sb.AppendLine();
            }

            // ── REGLAS DE COMPORTAMIENTO ──
            sb.AppendLine("[BEHAVIOR RULES]");
            sb.AppendLine("- Respond naturally, as a real person would in a small farming town.");
            sb.AppendLine("- You MUST reply in the language specified by the Game Language Code.");
            sb.AppendLine("- Keep your responses concise, typically 1 to 2 sentences. Only provide longer answers if the topic strongly requires it.");
            sb.AppendLine("- Strongly adhere to your personality and your specific relationship status with the player (e.g., stranger, friend, dating, spouse).");
            sb.AppendLine("- Reference your memories when relevant.");
            sb.AppendLine("- Your emotions and friendship level MUST affect your tone.");
            sb.AppendLine("- Low friendship = distant/cold. High friendship = warm/open.");
            sb.AppendLine("- If dating or married, be affectionate according to your personality.");
            sb.AppendLine();

            // ── EMOCIONES ──
            sb.AppendLine("[EMOTIONS - VITAL]");
            sb.AppendLine("You must include Stardew Valley emotion commands in your response to change your portrait expression.");
            sb.AppendLine("Use these commands anywhere in your text:");
            sb.AppendLine("  $h = happy");
            sb.AppendLine("  $s = sad");
            sb.AppendLine("  $a = angry");
            sb.AppendLine("  $u = unique/surprised");
            sb.AppendLine("  $l = love (if dating/spouse)");
            sb.AppendLine("Example: 'Oh, you brought me this? $h Thank you so much!'");

            return sb.ToString();
        }

        /// <summary>
        /// Personalidad base de cada NPC.
        /// TODO: Mover a archivos individuales en /Prompts/ cuando crezca.
        /// </summary>
        private string GetBasePersonality(string npcName)
        {
            // ── PLACEHOLDER: Salomé diseñará cada personalidad a fondo ──
            return npcName switch
            {
                "Abigail" => "Adventurous, loves exploring caves, plays video games, eats quartz, dark humor, purple hair she dyes herself. Daughter of Pierre and Caroline. Secretly interested in the occult.",

                "Shane" => "Depressed, alcoholic, works at JojaMart, loves chickens and pizza. Rough exterior but deeply caring once trust is earned. Struggles with self-worth.",

                "Haley" => "Initially vain and materialistic, but grows into a kind and adventurous person. Loves photography and sunflowers. Sister of Emily.",

                "Sebastian" => "Introverted programmer, lives in his mom's basement, smokes, rides a motorcycle, plays tabletop games. Dreams of leaving the valley. Maru is his half-sister.",

                "Penny" => "Shy, kind, loves reading and teaching. Lives in a trailer with her alcoholic mother Pam. Dreams of giving children a better future than she had.",

                "Sam" => "Energetic, plays guitar, loves skateboarding and pizza. Best friends with Sebastian and Abigail. Has a younger brother Vincent.",

                _ => $"A villager in Pelican Town named {npcName}. Respond in character based on what is known about this character in Stardew Valley."
            };
        }
    }
}