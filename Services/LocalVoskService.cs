using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using StardewModdingAPI;
using System.Text.Json;
using Vosk;
using LivingCompanionsValley.Utils;

namespace LivingCompanionsValley.Services
{
    public class LocalVoskService : IDisposable
    {
        private readonly IModHelper _helper;
        private readonly string _modDirectory;
        private string _modelPath = string.Empty;

        private Model? _model;
        private VoskRecognizer? _recognizer;
        private bool _isInitialized;
        private bool _resolverRegistered;
        private DllImportResolver? _resolver;

        public LocalVoskService(IModHelper helper)
        {
            _helper = helper;
            _modDirectory = _helper.DirectoryPath;

            RegisterDllImportResolver();
        }

        private void RegisterDllImportResolver()
        {
            if (_resolverRegistered) return;

            try
            {
                _resolver = (libraryName, assembly, searchPath) =>
                {
                    if (libraryName.Contains("libvosk", StringComparison.OrdinalIgnoreCase))
                    {
                        string voskPath = Path.Combine(_modDirectory, "libvosk.dll");
                        if (File.Exists(voskPath))
                        {
                            ModEntry.Logger?.Log($"[DllImportResolver] Interceptada petición de '{libraryName}'. Entregando ruta directa: {voskPath}", LogLevel.Trace);
                            if (NativeLibrary.TryLoad(voskPath, out IntPtr handle))
                            {
                                return handle;
                            }
                        }
                    }
                    return IntPtr.Zero; // Default behavior
                };

                NativeLibrary.SetDllImportResolver(typeof(Vosk.Model).Assembly, _resolver);
                _resolverRegistered = true;
                ModEntry.Logger?.Log("[DllImportResolver] Custom resolver registrado exitosamente en Vosk.", LogLevel.Info);
            }
            catch (Exception ex)
            {
                ModEntry.Logger?.Log($"[DllImportResolver] Error al registrar el resolver: {ex.Message}", LogLevel.Error);
            }
        }

        public async Task InitializeAsync()
        {
            if (_isInitialized) return;

            // Fase 1: Soporte Multilingüe
            // Se evalúa el idioma aquí porque el juego ya ha cargado sus preferencias de usuario (ej. OnSaveLoaded)
            string modelFolderName = StardewValley.LocalizedContentManager.CurrentLanguageCode == StardewValley.LocalizedContentManager.LanguageCode.es
                ? "vosk-model-small-es"
                : "vosk-model-small-en-us";
            _modelPath = Path.Combine(_modDirectory, "Assets", modelFolderName);

            try
            {
                if (!Directory.Exists(_modelPath))
                {
                    ModEntry.Logger?.Log($"[ERROR CRÍTICO] El directorio del modelo de Vosk no se encontró en: {_modelPath}. Por favor, coloca el modelo para activar el reconocimiento de voz local.", LogLevel.Error);
                    return;
                }

                ModEntry.Logger?.Log("Inicializando modelo de Vosk local...", LogLevel.Trace);

                await Task.Run(() =>
                {
                    // Vosk model loading is synchronous, so we run it in a background task
                    Vosk.Vosk.SetLogLevel(-1); // Disable Vosk spam logging
                    _model = new Model(_modelPath);
                    _recognizer = new VoskRecognizer(_model, 16000.0f);
                });

                _isInitialized = true;
                ModEntry.Logger?.Log("Vosk inicializado correctamente.", LogLevel.Info);
            }
            catch (Exception ex)
            {
                ModEntry.Logger?.Log($"Error crítico al inicializar Vosk: {ex.Message}\nStack: {ex.StackTrace}", LogLevel.Error);
            }
        }

        public async Task<string> TranscribeAudioAsync(byte[] pcmData)
        {
            if (!_isInitialized || _recognizer == null)
            {
                ModEntry.Logger?.Log("[Error] Vosk no está inicializado al intentar transcribir.", LogLevel.Error);
                return "[Error] Vosk no está inicializado.";
            }

            try
            {
                ModEntry.Logger?.Log($"Enviando {pcmData.Length} bytes de audio a Vosk...", LogLevel.Info);
                var stopwatch = System.Diagnostics.Stopwatch.StartNew();

                string resultJson = await Task.Run(() =>
                {
                    if (_recognizer.AcceptWaveform(pcmData, pcmData.Length))
                    {
                        return _recognizer.Result();
                    }
                    else
                    {
                        return _recognizer.FinalResult();
                    }
                }).ConfigureAwait(false);

                // Parse the JSON result {"text": "hello"}
                string finalTrimmedText = "";
                if (!string.IsNullOrWhiteSpace(resultJson))
                {
                    try
                    {
                        using var doc = JsonDocument.Parse(resultJson);
                        if (doc.RootElement.TryGetProperty("text", out var textProp))
                        {
                            finalTrimmedText = textProp.GetString()?.Trim() ?? "";
                        }
                    }
                    catch (JsonException ex)
                    {
                        ModEntry.Logger?.Log($"Error al parsear el JSON de Vosk: {ex.Message}", LogLevel.Error);
                    }
                }

                // Fase 2: Normalización Fonética
                if (!string.IsNullOrWhiteSpace(finalTrimmedText))
                {
                    finalTrimmedText = PhoneticNormalizer.NormalizeTranscript(finalTrimmedText, LoreRoutingService.KnownNpcs);
                }

                stopwatch.Stop();
                ModEntry.Logger?.Log($"Vosk devolvió '{finalTrimmedText}' en {stopwatch.ElapsedMilliseconds}ms.", LogLevel.Info);

                return finalTrimmedText;
            }
            catch (Exception ex)
            {
                ModEntry.Logger?.Log($"Error al transcribir con Vosk: {ex.Message}\n{ex.StackTrace}", LogLevel.Error);
                return "[Error] Excepción en transcripción de Vosk.";
            }
        }

        public void Dispose()
        {
            _recognizer?.Dispose();
            _model?.Dispose();
        }
    }
}
