using System;
using System.IO;
using System.Threading.Tasks;
using StardewModdingAPI;
using Whisper.net;
using Whisper.net.Ggml;

namespace LivingCompanionsValley.Services
{
    /// <summary>
    /// Servicio que envuelve la lógica asíncrona de Whisper.net para transcripción offline.
    /// Descarga automáticamente el modelo la primera vez que se necesita.
    /// </summary>
    public class LocalWhisperService : IDisposable
    {
        private WhisperFactory? _factory;
        private readonly string _modelPath;
        private bool _isInitialized;

        public LocalWhisperService(IModHelper helper)
        {
            _modelPath = Path.Combine(helper.DirectoryPath, "Assets", "ggml-base.bin");

            AppDomain.CurrentDomain.UnhandledException += (sender, e) =>
            {
                ModEntry.Logger?.Log($"[CRASH NATIVO WHISPER] {e.ExceptionObject}", LogLevel.Error);
            };
        }

        /// <summary>
        /// Inicializa el procesador de Whisper (descargando el modelo si no existe).
        /// </summary>
        public async Task InitializeAsync()
        {
            if (_isInitialized) return;

            try
            {
                if (!File.Exists(_modelPath))
                {
                    ModEntry.Logger?.Log("El modelo Whisper no se encontró. Descargando ggml-base.bin...", LogLevel.Info);

                    string? dir = Path.GetDirectoryName(_modelPath);
                    if (dir != null && !Directory.Exists(dir))
                        Directory.CreateDirectory(dir);

                    // ✅ Fix: new WhisperGgmlDownloader() en lugar de WhisperGgmlDownloader.Default
                    using var modelStream = await new WhisperGgmlDownloader().GetGgmlModelAsync(GgmlType.Base);
                    using var fileWriter = File.OpenWrite(_modelPath);
                    await modelStream.CopyToAsync(fileWriter);

                    ModEntry.Logger?.Log("Modelo descargado exitosamente.", LogLevel.Info);
                }

                ModEntry.Logger?.Log("Inicializando modelo de Whisper local (Factory)...", LogLevel.Trace);
                _factory = WhisperFactory.FromPath(_modelPath);
                _isInitialized = true;
                ModEntry.Logger?.Log("Whisper inicializado correctamente.", LogLevel.Trace);
            }
            catch (Exception ex)
            {
                ModEntry.Logger?.Log($"Error crítico al inicializar Whisper.net: {ex.Message}", LogLevel.Error);
            }
        }

        /// <summary>
        /// Transcribe un buffer de audio de 32-bit floats a texto.
        /// </summary>
        // ✅ Fix: eliminado [HandleProcessCorruptedStateExceptions] obsoleto en .NET 6
        public async Task<string> TranscribeAudioAsync(float[] floatAudioBuffer)
        {
            if (!_isInitialized || _factory == null)
            {
                ModEntry.Logger?.Log("[Error] Whisper no está inicializado al intentar transcribir.", LogLevel.Error);
                return "[Error] Whisper no está inicializado.";
            }

            using var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(15));

            try
            {
                ModEntry.Logger?.Log($"Enviando {floatAudioBuffer.Length} muestras de audio a Whisper...", LogLevel.Info);
                var stopwatch = System.Diagnostics.Stopwatch.StartNew();

                string finalTrimmedText = await Task.Run(async () =>
                {
                    ModEntry.Logger?.Log("Creando processor efímero de Whisper.net...", LogLevel.Trace);

                    await using var processor = _factory.CreateBuilder()
                        .WithLanguage("es")
                        .WithNoContext()
                        .Build();

                    var sb = new System.Text.StringBuilder();

                    ModEntry.Logger?.Log("Iniciando ProcessAsync con Whisper.net...", LogLevel.Trace);

                    await foreach (var segment in processor.ProcessAsync(floatAudioBuffer, cts.Token))
                    {
                        sb.Append(segment.Text);
                        ModEntry.Logger?.Log($"Segmento transcrito: '{segment.Text}'", LogLevel.Trace);
                    }

                    return sb.ToString().Trim();

                }, cts.Token).ConfigureAwait(false);

                stopwatch.Stop();
                ModEntry.Logger?.Log($"Whisper devolvió '{finalTrimmedText}' en {stopwatch.ElapsedMilliseconds}ms.", LogLevel.Info);

                return finalTrimmedText;
            }
            catch (OperationCanceledException)
            {
                ModEntry.Logger?.Log("[Error] Timeout (15s) al transcribir con Whisper.", LogLevel.Error);
                return "[Error] Tiempo de espera agotado al transcribir.";
            }
            catch (AccessViolationException ex)
            {
                ModEntry.Logger?.Log($"[CRASH NATIVO] AccessViolation en Whisper: {ex}", LogLevel.Error);
                return "[Error] Crash de memoria al transcribir.";
            }
            catch (System.Runtime.InteropServices.SEHException ex)
            {
                ModEntry.Logger?.Log($"[CRASH NATIVO] SEHException en Whisper: {ex}", LogLevel.Error);
                return "[Error] Fallo interno del motor Whisper.";
            }
            catch (Exception ex)
            {
                ModEntry.Logger?.Log($"Error al transcribir: {ex.Message}\n{ex.StackTrace}", LogLevel.Error);
                return "[Error] Excepción en transcripción de Whisper.";
            }
        }

        public void Dispose()
        {
            _factory?.Dispose();
        }
    }
}