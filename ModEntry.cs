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

        /// <summary>
        /// El método de entrada invocado por SMAPI.
        /// </summary>
        /// <param name="helper">Proporciona métodos simplificados para interactuar con SMAPI.</param>
        public override void Entry(IModHelper helper)
        {
            // Guardar referencia del monitor para ser usada globalmente
            Logger = this.Monitor;

            AppDomain.CurrentDomain.UnhandledException += (sender, e) =>
            {
                Logger?.Log($"[CRASH NO CONTROLADO] {e.ExceptionObject}", LogLevel.Error);
            };

            // ✅ CRÍTICO: cargar whisper.dll nativo ANTES de instanciar cualquier servicio de Whisper
            PreloadNativeWhisper(helper);

            // Cargar configuración pública
            _config = helper.ReadConfig<ModConfig>();

            // Cargar configuración secreta manualmente usando Data API
            _secretConfig = helper.Data.ReadJsonFile<SecretConfig>("SecretConfig.json") ?? new SecretConfig();
            
            // Si la key está vacía, guardamos el archivo para que el usuario pueda editarlo
            if (string.IsNullOrWhiteSpace(_secretConfig.VeniceApiKey))
            {
                helper.Data.WriteJsonFile("SecretConfig.json", _secretConfig);
                Logger.Log("Se ha creado 'SecretConfig.json'. Por favor, añade tu Venice API Key en ese archivo.", LogLevel.Warn);
            }

            // Inicializar servicios principales
            _veniceApiService = new VeniceApiService(_secretConfig.VeniceApiKey);
            _voiceManager = new VoiceInteractionManager(helper, _config, _veniceApiService);

            // Log de inicio exitoso
            Logger.Log("Living Companions Valley loaded successfully.", LogLevel.Info);

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

        private void PreloadNativeWhisper(IModHelper helper)
{
    var modDir = helper.DirectoryPath;

    // Cargar las dependencias de ggml primero, en orden,
    // para que Windows no busque versiones incorrectas en el sistema
    var dlls = new[]
    {
        "ggml-base-whisper.dll",
        "ggml-cpu-whisper.dll",
        "ggml-whisper.dll",
        "whisper.dll"
    };

    foreach (var dllName in dlls)
    {
        var fullPath = Path.Combine(modDir, dllName);
        try
        {
            if (!File.Exists(fullPath))
            {
                Logger?.Log($"[Whisper] No encontrado: {dllName}", LogLevel.Warn);
                continue;
            }
            System.Runtime.InteropServices.NativeLibrary.Load(fullPath);
            Logger?.Log($"[Whisper] Cargado: {dllName}", LogLevel.Info);
        }
        catch (Exception ex)
        {
            Logger?.Log($"[Whisper] Error cargando {dllName}: {ex.Message}", LogLevel.Warn);
        }
    }
}