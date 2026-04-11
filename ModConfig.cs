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
        //  WHISPER AI — Modelo Local
        // ═══════════════════════════════════════

        /// <summary>Ruta al modelo Whisper local.</summary>
        public string WhisperModelPath { get; set; } = "assets/ggml-base.bin";

        // ═══════════════════════════════════════
        //  MEMORIA DE NPCs
        // ═══════════════════════════════════════

        /// <summary>Máximo de recuerdos por NPC.</summary>
        public int MaxMemoryPerNpc { get; set; } = 50;

        /// <summary>Guardar memorias entre sesiones.</summary>
        public bool PersistMemory { get; set; } = true;

        // ═══════════════════════════════════════
        //  CONTROLES DE VOZ
        // ═══════════════════════════════════════

        /// <summary>Tecla para grabar voz (mantener presionada).</summary>
        public string VoiceKey { get; set; } = "Tab";

        /// <summary>Distancia máxima (en tiles) para que un NPC te escuche.</summary>
        public float MaxInteractionDistance { get; set; } = 4.0f;
    }
}
