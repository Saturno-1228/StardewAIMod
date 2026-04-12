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
            // Ruta del modelo en la carpeta de assets
            _modelPath = Path.Combine(helper.DirectoryPath, "Assets", "ggml-base.bin");
        }

        /// <summary>
        /// Inicializa el procesador de Whisper (descargando el modelo si no existe).
        /// </summary>
        public async Task InitializeAsync()
        {
            if (_isInitialized) return;

            if (!File.Exists(_modelPath))
            {
                ModEntry.Logger?.Log(
                    $"[ERROR] Modelo Whisper no encontrado en: {_modelPath}\n" +
                    "Descarga 'ggml-base.bin' desde https://huggingface.co/sandrohanea/whisper.net/tree/main " +
                    "y colócalo en la carpeta Assets/ del mod.",
                    LogLevel.Error);
                return; // No intentar nada más
            }

            try
            {
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
        [System.Runtime.ExceptionServices.HandleProcessCorruptedStateExceptions]
        public async Task<string> TranscribeAudioAsync(float[] floatAudioBuffer)
        {
            if (!_isInitialized || _factory == null)
            {
                ModEntry.Logger?.Log("[Error] Whisper no está inicializado al intentar transcribir.", LogLevel.Error);
                return "[Error] Whisper no está inicializado.";
            }

            // Timeout de 15 segundos para evitar que Whisper se quede colgado
            using var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(15));

            try
            {
                ModEntry.Logger?.Log($"Enviando {floatAudioBuffer.Length} muestras de audio a Whisper para transcribir...", LogLevel.Info);
                var stopwatch = System.Diagnostics.Stopwatch.StartNew();
                
                // Forzar ejecución en ThreadPool, fuera del hilo de MonoGame
                string finalTrimmedText = await Task.Run(async () =>
                {
                    ModEntry.Logger?.Log("Creando processor efímero de Whisper.net...", LogLevel.Trace);

                    // El processor maneja estado interno nativo y NO es thread-safe ni reusable.
                    // Se debe instanciar por cada proceso y desechar usando async IDisposable (await using).
                    await using var processor = _factory.CreateBuilder()
                        .WithLanguage("es")
                        .WithNoContext() // Evitar corromper memoria con contexto pasado
                        .Build();

                    var sb = new System.Text.StringBuilder();

                    ModEntry.Logger?.Log("Iniciando ProcessAsync con Whisper.net...", LogLevel.Trace);

                    await foreach (var segment in processor.ProcessAsync(floatAudioBuffer, cts.Token))
                    {
                        sb.Append(segment.Text);
                        ModEntry.Logger?.Log($"Segmento transcrito detectado: '{segment.Text}'", LogLevel.Trace);
                    }

                    return sb.ToString().Trim();
                }, cts.Token).ConfigureAwait(false);
                
                stopwatch.Stop();
                ModEntry.Logger?.Log($"Whisper devolvió el texto '{finalTrimmedText}' en {stopwatch.ElapsedMilliseconds}ms.", LogLevel.Info);

                return finalTrimmedText;
            }
            catch (OperationCanceledException)
            {
                ModEntry.Logger?.Log("[Error] Tiempo de espera agotado (15s) al transcribir con Whisper. Posible cuelgue del modelo.", LogLevel.Error);
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
                ModEntry.Logger?.Log($"Error al transcribir el audio con Whisper: {ex.Message}\n{ex.StackTrace}", LogLevel.Error);
                return "[Error] Excepción en transcripción de Whisper.";
            }
        }

        public void Dispose()
        {
            _factory?.Dispose();
        }
    }
}
