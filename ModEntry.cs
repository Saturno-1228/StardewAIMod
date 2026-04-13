using System;
using System.IO;
using System.Runtime.InteropServices;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using LivingCompanionsValley.Configuration;
using LivingCompanionsValley.Services;

namespace LivingCompanionsValley
{
    public class ModEntry : Mod
    {
        internal static IMonitor? Logger { get; private set; }

        private ModConfig? _config;
        private SecretConfig? _secretConfig;
        private VoiceInteractionManager? _voiceManager;
        private VeniceApiService? _veniceApiService;

        // Importación para añadir el directorio del mod a la búsqueda de DLLs de Windows
        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern bool SetDllDirectory(string lpPathName);

        public override void Entry(IModHelper helper)
        {
            Logger = this.Monitor;

            AppDomain.CurrentDomain.UnhandledException += (sender, e) =>
            {
                Logger?.Log($"[CRASH NO CONTROLADO] {e.ExceptionObject}", LogLevel.Error);
            };

            // 1. Configurar búsqueda de DLLs en el directorio del mod
            var modDir = helper.DirectoryPath;
            SetDllDirectory(modDir);
            Logger?.Log($"[Whisper] Ruta de búsqueda de DLLs configurada: {modDir}", LogLevel.Info);

            // 2. Cargar manualmente las librerías nativas
            LoadNativeLibraries(modDir);

            // 3. Inicializar servicios
            _config = helper.ReadConfig<ModConfig>();
            _secretConfig = helper.Data.ReadJsonFile<SecretConfig>("SecretConfig.json") ?? new SecretConfig();

            if (string.IsNullOrWhiteSpace(_secretConfig.VeniceApiKey))
            {
                helper.Data.WriteJsonFile("SecretConfig.json", _secretConfig);
                Logger.Log("Se ha creado 'SecretConfig.json'. Añade tu Venice API Key.", LogLevel.Warn);
            }

            _veniceApiService = new VeniceApiService(_secretConfig.VeniceApiKey);
            _voiceManager = new VoiceInteractionManager(helper, _config, _veniceApiService);

            Logger.Log("Living Companions Valley loaded successfully.", LogLevel.Info);

            helper.Events.GameLoop.GameLaunched += OnGameLaunched;
        }

        /// <summary>
        /// Carga explícita de las DLLs nativas para evitar errores de resolución.
        /// </summary>
        private void LoadNativeLibraries(string modDir)
        {
            // El orden es vital: ggml (implementación) antes que whisper (API)
            var libs = new[] { "ggml-whisper.dll", "whisper.dll" };

            foreach (var lib in libs)
            {
                var path = Path.Combine(modDir, lib);
                if (File.Exists(path))
                {
                    try
                    {
                        var handle = NativeLibrary.Load(path);
                        Logger?.Log($"[Whisper] Cargado con éxito: {lib}", LogLevel.Info);
                    }
                    catch (DllNotFoundException)
                    {
                        Logger?.Log($"[Whisper] ERROR CRÍTICO: Falta una dependencia para {lib}.", LogLevel.Error);
                        Logger?.Log("Instala 'Visual C++ Redistributable 2015-2022 x64' desde microsoft.com", LogLevel.Error);
                    }
                    catch (BadImageFormatException)
                    {
                        Logger?.Log($"[Whisper] ERROR: {lib} es de arquitectura incorrecta (necesitas x64).", LogLevel.Error);
                    }
                    catch (Exception ex)
                    {
                        Logger?.Log($"[Whisper] Error desconocido cargando {lib}: {ex.Message}", LogLevel.Error);
                    }
                }
                else
                {
                    Logger?.Log($"[Whisper] Archivo no encontrado: {lib}", LogLevel.Warn);
                }
            }
        }

        private void OnGameLaunched(object? sender, GameLaunchedEventArgs e)
        {
        }
    }
}