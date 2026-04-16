using System.Text;
using LivingCompanionsValley.Models;

namespace LivingCompanionsValley.Services
{
    public class PromptBuilderService
    {
        public string BuildSystemPrompt(string npcName, string environmentContext, string recentMemory)
        {
            var sb = new StringBuilder();

            // Intentar obtener la identidad desde la caché RAM
            if (!LoreRoutingService.NpcIdentityCache.TryGetValue(npcName, out NpcIdentityDto identity))
            {
                // Fallback si no existe en la caché
                identity = new NpcIdentityDto
                {
                    NpcName = npcName,
                    TonalStyle = "Hablas de forma casual y educada.",
                    SystemPrompt = $"Eres {npcName}. Responde de forma natural a las preguntas o comentarios del granjero."
                };
            }

            // --- [IDENTIDAD] ---
            sb.AppendLine("[IDENTIDAD]");
            sb.AppendLine($"SystemPrompt: {identity.SystemPrompt}");
            sb.AppendLine($"TonalStyle: {identity.TonalStyle}");
            sb.AppendLine();

            // --- [ENTORNO] ---
            sb.AppendLine("[ENTORNO]");
            sb.AppendLine(environmentContext);
            sb.AppendLine();

            // --- [MEMORIA RECIENTE] ---
            sb.AppendLine("[MEMORIA RECIENTE]");
            if (string.IsNullOrWhiteSpace(recentMemory))
            {
                sb.AppendLine("Ninguna conversación reciente.");
            }
            else
            {
                sb.AppendLine(recentMemory);
            }
            sb.AppendLine();

            // --- [REGLAS DEL SISTEMA] ---
            sb.AppendLine("[REGLAS DEL SISTEMA]");
            sb.AppendLine("Regla absoluta: Responde a la última interacción del granjero en 1 o 2 oraciones cortas como máximo. Mantente estrictamente en personaje.");

            return sb.ToString().TrimEnd();
        }
    }
}
