using System.Text.Json.Serialization;

namespace StardewLivingValley.Configuration
{
    /// <summary>
    /// Configuraciones secretas que no deben subirse al repositorio,
    /// como claves de API (ej. Venice API Key).
    /// </summary>
    public class SecretConfig
    {
        // Ejemplo comentado:
        // [JsonIgnore] no se usaría aquí si queremos serializar/deserializar el JSON secreto con SMAPI,
        // más bien confiamos en que .gitignore evite que este archivo suba al control de versiones.

        // public string VeniceApiKey { get; set; } = "";

        public SecretConfig()
        {
        }
    }
}
