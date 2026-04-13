using System;
using System.IO;
using System.Diagnostics;
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

        public override void Entry(IModHelper helper)
        {
            Logger = this.Monitor;

            AppDomain.CurrentDomain.UnhandledException += (sender, e) =>
            {
                Logger?.Log($"[CRASH NO CONTROLADO] {e.ExceptionObject}", LogLevel.Error);
            };

            // 1. Asegurar que las librerías nativas estén accesibles
            EnsureNativeLibs(helper);

            // 2. Configurar servicios
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

        private void OnGameLaunched(object? sender, GameLaunchedEventArgs e)
        {
            // Espacio para futuras fases.
        }

        /// <summary>
        /// Copia las librerías nativas al directorio raíz del juego para que el SO las encuentre.
        /// </summary>
        private void EnsureNativeLibs(IModHelper helper)
        {
            var modDir = helper.DirectoryPath;
            // El directorio base es donde está StardewValley.exe
            var gameDir = AppDomain.CurrentDomain.BaseDirectory; 

            var libs = new[] { "ggml-whisper.dll", "whisper.dll" };

            foreach (var lib in libs)
            {
                var source = Path.Combine(modDir, lib);
                var dest = Path.Combine(gameDir, lib);

                if (File.Exists(source))
                {
                    try
                    {
                        // Copiar si el destino no existe o es más antiguo
                        if (!File.Exists(dest) || File.GetLastWriteTime(source) > File.GetLastWriteTime(dest))
                        {
                            File.Copy(source, dest, overwrite: true);
                            Logger?.Log($"[Whisper] Copiado {lib} a directorio del juego.", LogLevel.Info);
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger?.Log($"[Whisper] Error copiando {lib} (posible falta de permisos): {ex.Message}", LogLevel.Error);
                        Logger?.Log($"[Whisper] Solución: Ejecuta Steam como Administrador o copia manualmente {lib} a {gameDir}", LogLevel.Error);
                    }
                }
                else
                {
                    Logger?.Log($"[Whisper] No se encontró {lib} en el mod. Verifica la compilación.", LogLevel.Warn);
                }
            }
        }
    }
}