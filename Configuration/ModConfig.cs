using StardewModdingAPI;

namespace StardewLivingValley.Configuration
{
    /// <summary>
    /// Configuraciones generales del mod, accesibles por el jugador.
    /// Aquí se colocarán en el futuro las configuraciones para integrarse con GenericModConfigMenu.
    /// </summary>
    public class ModConfig
    {
        /// <summary>
        /// Tecla utilizada para iniciar la captura de voz (Push-To-Talk).
        /// </summary>
        public SButton VoiceKey { get; set; }

        public ModConfig()
        {
            // Valor por defecto: Tecla Tab
            this.VoiceKey = SButton.Tab;
        }
    }
}