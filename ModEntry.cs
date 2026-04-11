using StardewModdingAPI;
using StardewModdingAPI.Events;

namespace StardewLivingValley
{
    /// <summary>
    /// El punto de entrada principal para el mod Stardew Living Valley.
    /// </summary>
    public class ModEntry : Mod
    {
        /// <summary>
        /// Instancia estática del Logger para uso global en la aplicación.
        /// </summary>
        internal static IMonitor? Logger { get; private set; }

        /// <summary>
        /// El método de entrada invocado por SMAPI.
        /// </summary>
        /// <param name="helper">Proporciona métodos simplificados para interactuar con SMAPI.</param>
        public override void Entry(IModHelper helper)
        {
            // Guardar referencia del monitor para ser usada globalmente
            Logger = this.Monitor;

            // Log de inicio exitoso (fase 0)
            Logger.Log("Stardew Living Valley loaded.", LogLevel.Info);

            // Preparación de eventos base
            helper.Events.GameLoop.GameLaunched += OnGameLaunched;
        }

        /// <summary>
        /// Evento que se dispara después de que el juego es lanzado.
        /// Ideal para cargar APIs externas, GenericModConfigMenu, etc.
        /// </summary>
        private void OnGameLaunched(object? sender, GameLaunchedEventArgs e)
        {
            // Espacio preparado para futuras fases.
        }
    }
}
