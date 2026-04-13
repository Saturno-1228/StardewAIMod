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

        [System.Runtime.InteropServices.DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool SetDllDirectory(string lpPathName);

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

            if (SetDllDirectory(modDir))
            {
                Logger?.Log($"[Whisper] DLL search path configurado: {modDir}", LogLevel.Info);
            }
            else
            {
                Logger?.Log("[Whisper] No se pudo configurar DLL search path.", LogLevel.Warn);
            }
        }
    }
}