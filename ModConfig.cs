namespace StardewAIMod
{
    /// <summary>
    /// Configuración general del mod.
    /// SMAPI genera config.json automáticamente basado en esta clase.
    /// Este archivo ES SEGURO para GitHub.
    /// </summary>
    public class ModConfig
    {
        // ═══════════════════════════════════════
        //  VENICE AI — Modelo y Endpoint
        // ═══════════════════════════════════════

        /// <summary>Modelo de lenguaje a usar.</summary>
        public string VeniceModel { get; set; } = "zai-org-glm-5";

        /// <summary>Endpoint base de Venice API.</summary>
        public string VeniceEndpoint { get; set; } = "https://api.venice.ai/api/v1/chat/completions";

        // ═══════════════════════════════════════
        //  FUNCIONALIDADES
        // ═══════════════════════════════════════

        /// <summary>Habilitar chat por texto.</summary>
        public bool EnableTextChat { get; set; } = true;

        /// <summary>Habilitar entrada/salida de voz (futuro).</summary>
        public bool EnableVoice { get; set; } = false;

        // ═══════════════════════════════════════
        //  MEMORIA DE NPCs
        // ═══════════════════════════════════════

        /// <summary>Máximo de recuerdos por NPC.</summary>
        public int MaxMemoryPerNpc { get; set; } = 50;

        /// <summary>Guardar memorias entre sesiones.</summary>
        public bool PersistMemory { get; set; } = true;

        // ═══════════════════════════════════════
        //  CONTROLES
        // ═══════════════════════════════════════

        /// <summary>Tecla para abrir chat con NPC cercano.</summary>
        public string ChatKey { get; set; } = "N";
    }
}