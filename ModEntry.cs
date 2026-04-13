using System;
using System.IO;
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

            PreloadNativeWhisper(helper);

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
        }

        private void PreloadNativeWhisper(IModHelper helper)
        {
            var modDir = helper.DirectoryPath;
            var dlls = new[] { "ggml-whisper.dll", "whisper.dll" };

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
    }
}