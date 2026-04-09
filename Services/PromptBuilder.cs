using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using StardewAIMod.Models;

namespace StardewAIMod.Services
{
    /// <summary>
    /// Construye los system prompts para cada NPC.
    /// Combina: personalidad base + memorias + contexto del momento.
    /// </summary>
    public class PromptBuilder
    {
        private readonly string _modDirectory;
        private readonly Dictionary<string, string> _personalityCache = new Dictionary<string, string>();
        private readonly Dictionary<string, LoreData> _loreCache = new Dictionary<string, LoreData>();

        public PromptBuilder(string modDirectory)
        {
            _modDirectory = modDirectory;
        }

        private string ExtractRelevantLore(string npcName, string playerMessage)
        {
            if (string.IsNullOrWhiteSpace(playerMessage))
                return "";

            var relevantLore = new List<string>();

            // Load General Lore
            var generalLore = LoadLore("General.json");
            foreach (var topic in generalLore.Topics)
            {
                foreach (var keyword in topic.Keywords)
                {
                    string pattern = @"\b" + System.Text.RegularExpressions.Regex.Escape(keyword) + @"\b";
                    if (System.Text.RegularExpressions.Regex.IsMatch(playerMessage, pattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase))
                    {
                        relevantLore.Add($"- {topic.Info}");
                        break; // Prevent adding the same topic multiple times if multiple keywords match
                    }
                }
            }

            // Load NPC Specific Lore
            var npcLore = LoadLore($"{npcName}.json");
            foreach (var topic in npcLore.Topics)
            {
                foreach (var keyword in topic.Keywords)
                {
                    string pattern = @"\b" + System.Text.RegularExpressions.Regex.Escape(keyword) + @"\b";
                    if (System.Text.RegularExpressions.Regex.IsMatch(playerMessage, pattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase))
                    {
                        relevantLore.Add($"- {topic.Info}");
                        break;
                    }
                }
            }

            if (relevantLore.Count > 0)
            {
                var sb = new StringBuilder();
                sb.AppendLine("[RELEVANT KNOWLEDGE]");
                sb.AppendLine("Here is some specific knowledge you possess about topics the player just mentioned:");
                foreach (var lore in relevantLore)
                {
                    sb.AppendLine(lore);
                }
                sb.AppendLine();
                return sb.ToString();
            }

            return "";
        }

        private LoreData LoadLore(string loreFileName)
        {
            if (_loreCache.TryGetValue(loreFileName, out LoreData cachedLore))
            {
                return cachedLore;
            }

            string lorePath = Path.Combine(_modDirectory, "Prompts", "Lore", loreFileName);
            if (File.Exists(lorePath))
            {
                try
                {
                    string json = File.ReadAllText(lorePath);
                    var loreData = JsonSerializer.Deserialize<LoreData>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    if (loreData != null)
                    {
                        _loreCache[loreFileName] = loreData;
                        return loreData;
                    }
                }
                catch
                {
                    // Ignore errors, return empty
                }
            }

            var emptyLore = new LoreData();
            _loreCache[loreFileName] = emptyLore;
            return emptyLore;
        }

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
            Dictionary<string, string> currentContext,
            string playerMessage)
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

                // Enviar solo un subconjunto reciente o limitado para no exceder los tokens.
                // Ordenamos por fecha o asumimos que ya vienen en orden.
                // Tomamos un máximo de las últimas 10 memorias (por ejemplo) para evitar desbordar el contexto
                int maxMemoriesToInclude = 10;
                var memorias = memory.Memories;
                int startIdx = System.Math.Max(0, memorias.Count - maxMemoriesToInclude);

                for (int i = startIdx; i < memorias.Count; i++)
                {
                    var mem = memorias[i];
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

            // ── RELEVANT LORE / KNOWLEDGE ──
            string loreText = ExtractRelevantLore(npcName, playerMessage);
            if (!string.IsNullOrEmpty(loreText))
            {
                sb.AppendLine(loreText);
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

            // ── ACTIONS ──
            sb.AppendLine("[ACTIONS]");
            sb.AppendLine("You can perform special actions by including command tags in your response.");
            sb.AppendLine("  $follow = Agree to follow the player if they ask and you like them enough (minimum 'Best Friend' or 'Friend' depending on your personality). Do NOT use this if you are busy or dislike them.");
            sb.AppendLine("  $favor[ItemName, Amount, \"Letter Text\"] = If the player asks for a favor to get an item, and you have at least 'Friend' status, AND the item matches your 'Expertise' or 'Profession', you can agree to get it for them.");
            sb.AppendLine("    - You will mail it to them the next day.");
            sb.AppendLine("    - IMPORTANT: Include this command silently at the end of your message. Provide the actual English name of the item.");
            sb.AppendLine("    - Make sure to provide a short letter text to accompany the delivery inside the quotes.");
            sb.AppendLine("Example: 'I'll find some wood for you this afternoon. $favor[Wood, 20, \"Here is the wood you asked for yesterday. Watch out for splinters! -Robin\"]'");
            sb.AppendLine();

            // ── EMOCIONES ──
            sb.AppendLine("[EMOTIONS & FRIENDSHIP - VITAL]");
            sb.AppendLine("You must include Stardew Valley emotion commands in your response to change your portrait expression.");
            sb.AppendLine("These commands directly affect the player's friendship points with you in-game:");
            sb.AppendLine("  $h = happy (Use if the player is kind, friendly, or helpful. Increases friendship)");
            sb.AppendLine("  $l = love (Use if the player is romantic and you are dating/married. Greatly increases friendship)");
            sb.AppendLine("  $a = angry (Use if the player is rude, insulting, or annoying. Decreases friendship)");
            sb.AppendLine("  $s = sad (Situational. No friendship change)");
            sb.AppendLine("  $u = unique/surprised (Situational. No friendship change)");
            sb.AppendLine("Example: 'Oh, you brought me this? $h Thank you so much!'");

            return sb.ToString();
        }

        /// <summary>
        /// Personalidad base de cada NPC.
        /// Carga desde archivos individuales en /Prompts/Personalities/.
        /// </summary>
        private string GetBasePersonality(string npcName)
        {
            if (_personalityCache.TryGetValue(npcName, out string cachedPersonality))
            {
                return cachedPersonality;
            }

            string promptsDir = Path.Combine(_modDirectory, "Prompts", "Personalities");
            string filePath = Path.Combine(promptsDir, $"{npcName}.json");
            string defaultPath = Path.Combine(promptsDir, "Default.json");

            string targetPath = File.Exists(filePath) ? filePath : (File.Exists(defaultPath) ? defaultPath : null);

            if (targetPath != null)
            {
                try
                {
                    string json = File.ReadAllText(targetPath);
                    using JsonDocument doc = JsonDocument.Parse(json);

                    var sb = new StringBuilder();
                    if (doc.RootElement.TryGetProperty("Personality", out JsonElement prop))
                    {
                        string personality = prop.GetString();
                        if (targetPath == defaultPath)
                        {
                            // Insert NPC name in the default personality text
                            personality = personality.Replace("A villager", $"A villager named {npcName}");
                        }
                        sb.AppendLine(personality);
                    }

                    if (doc.RootElement.TryGetProperty("Profession", out JsonElement profProp))
                        sb.AppendLine($"Profession: {profProp.GetString()}");

                    if (doc.RootElement.TryGetProperty("Expertise", out JsonElement expProp))
                    {
                        // To prevent LLM hallucinations, enforce only existing item names.
                        string expertiseRaw = expProp.GetString();
                        sb.AppendLine($"Expertise / Items you can provide: {expertiseRaw}");
                        sb.AppendLine($"(CRITICAL: You must only offer specific item names if you know they exist. Some examples from your world: {string.Join(", ", System.Linq.Enumerable.Take(StardewAIMod.ModEntry.ItemCatalog.Keys, 10))})");
                    }

                    if (doc.RootElement.TryGetProperty("MapKnowledge", out JsonElement mapProp))
                        sb.AppendLine($"Map Knowledge: {mapProp.GetString()}");

                    string finalPersonality = sb.ToString().TrimEnd();
                    _personalityCache[npcName] = finalPersonality;
                    return finalPersonality;
                }
                catch
                {
                    // Ignore parsing errors and fallback
                }
            }

            return $"A villager in Pelican Town named {npcName}. Respond in character based on what is known about this character in Stardew Valley.";
        }
    }
}