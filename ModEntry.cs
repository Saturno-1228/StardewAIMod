using System;
using System.IO;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using LivingCompanionsValley.Configuration;
using LivingCompanionsValley.Services;
using Whisper.net;

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

            try
            {
                var whisperAssembly = typeof(WhisperFactory).Assembly;

                System.Runtime.InteropServices.NativeLibrary.SetDllImportResolver(
                    whisperAssembly,
                    (libraryName, assembly, searchPath) =>
                    {
                        var candidates = new[]
                        {
                            Path.Combine(modDir, $"{libraryName}.dll"),
                            Path.Combine(modDir, libraryName),
                        };

                        foreach (var candidate in candidates)
                        {
                            if (File.Exists(candidate))
                            {
                                if (System.Runtime.InteropServices.NativeLibrary.TryLoad(candidate, out var handle))
                                {
                                    Logger?.Log($"[Whisper] Resuelto '{libraryName}' -> {Path.GetFileName(candidate)}", LogLevel.Info);
                                    return handle;
                                }
                            }
                        }

                        Logger?.Log($"[Whisper] No resuelto: '{libraryName}'", LogLevel.Warn);
                        return IntPtr.Zero;
                    });

                Logger?.Log("[Whisper] DllImportResolver configurado.", LogLevel.Info);
            }
            catch (InvalidOperationException)
            {
                Logger?.Log("[Whisper] Resolver ya existía — Whisper.net se inicializó antes de Entry().", LogLevel.Warn);
            }
            catch (Exception ex)
            {
                Logger?.Log($"[Whisper] Error configurando resolver: {ex.Message}", LogLevel.Error);
            }
        }
    }
}