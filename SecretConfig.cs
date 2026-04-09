namespace StardewAIMod
{
    /// <summary>
    /// Credenciales sensibles que NUNCA deben subirse a GitHub.
    /// Se cargan desde secrets.json
    /// </summary>
    public class SecretConfig
    {
        /// <summary>API Key de Venice AI.</summary>
        public string VeniceApiKey { get; set; } = "";

        /// <summary>Clave de suscripción del usuario (DIEM).</summary>
        public string SubscriptionKey { get; set; } = "";
    }
}