using System.Threading.Tasks;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using LivingCompanionsValley.Configuration;
using LivingCompanionsValley.Services;

namespace LivingCompanionsValley
{
    /// <summary>
    /// El punto de entrada principal para el mod Living Companions Valley.
    /// </summary>
    public class ModEntry : Mod
    {
        /// <summary>
        /// Instancia estática del Logger para uso global en la aplicación.
        /// </summary>
        internal static IMonitor? Logger { get; private set; }

        private ModConfig? _config;
        private SecretConfig? _secretConfig;
        private VoiceInteractionManager? _voiceManager;
        private VeniceApiService? _veniceApiService;
        private DatabaseService? _databaseService;
        private LoreRoutingService? _loreRoutingService;

        /// <summary>
        /// El método de entrada invocado por SMAPI.
        /// </summary>
        /// <param name="helper">Proporciona métodos simplificados para interactuar con SMAPI.</param>
        public override void Entry(IModHelper helper)
        {
            // Guardar referencia del monitor para ser usada globalmente
            Logger = this.Monitor;

            // Cargar configuración pública
            _config = helper.ReadConfig<ModConfig>();

            // Cargar configuración secreta manualmente usando Data API
            _secretConfig = helper.Data.ReadJsonFile<SecretConfig>("SecretConfig.json") ?? new SecretConfig();

            // Si la key está vacía, guardamos el archivo para que el usuario pueda editarlo
            if (string.IsNullOrWhiteSpace(_secretConfig.VeniceApiKey))
            {
                helper.Data.WriteJsonFile("SecretConfig.json", _secretConfig);
                Logger!.Log("Se ha creado 'SecretConfig.json'. Por favor, añade tu Venice API Key en ese archivo.", LogLevel.Warn);
            }

            // Inicializar servicios principales
            _databaseService = new DatabaseService(this.Helper.DirectoryPath);
            _loreRoutingService = new LoreRoutingService(Logger!, _databaseService);
            _veniceApiService = new VeniceApiService(_secretConfig.VeniceApiKey);
            _voiceManager = new VoiceInteractionManager(helper, _config, _veniceApiService);

            // Log de inicio exitoso
            Logger!.Log("Living Companions Valley loaded successfully.", LogLevel.Info);

            // Preparación de eventos base
            helper.Events.GameLoop.SaveLoaded += OnSaveLoaded;
        }

        private void OnSaveLoaded(object? sender, SaveLoadedEventArgs e)
        {
            // 1. Iniciar la ingesta de Lore (Base de datos)
            _ = _loreRoutingService?.IngestAllLoreAsync(this.Helper.DirectoryPath);

            // 2. Inicializar el motor de Voz de forma asíncrona
            Task.Run(async () =>
            {
                if (_voiceManager != null)
                {
                    await _voiceManager.InitializeVoskAsync();
                }
            });
        }
    }
}