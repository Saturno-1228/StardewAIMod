using System.Text.Json.Serialization;

namespace LivingCompanionsValley.Configuration
{
    /// <summary>
    /// Configuraciones secretas que no deben subirse al repositorio,
    /// como claves de API (ej. Venice API Key).
    /// </summary>
    public class SecretConfig
    {
        /// <summary>
        /// La clave API de Venice AI para autenticar las peticiones.
        /// </summary>
        public string VeniceApiKey { get; set; } = "";

        public SecretConfig()
        {
        }
    }
}
