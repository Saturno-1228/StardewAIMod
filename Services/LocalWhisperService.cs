using System;
using System.IO;
using System.Threading.Tasks;
using StardewModdingAPI;
using Whisper.net;
using Whisper.net.Ggml;

namespace LivingCompanionsValley.Services
{
    public class LocalWhisperService : IDisposable
    {
        private WhisperFactory? _factory;
        private readonly string _modelPath;
        private bool _isInitialized;

        public LocalWhisperService(IModHelper helper)
        {
            _modelPath = Path.Combine(helper.DirectoryPath, "Assets", "ggml-base.bin");
        }

        public async Task InitializeAsync()
        {
            if (_isInitialized) return;

            try
            {
                if (!File.Exists(_modelPath))
                {
                    ModEntry.Logger?.Log(
                        "Modelo Whisper no encontrado. Descargando ggml-base.bin (~142 MB)... " +
                        "Esto solo ocurre una vez.", LogLevel.Info);

                    StardewValley.Game1.addHUDMessage(new StardewValley.HUDMessage(
                        "Living Companions: Descargando modelo de voz (~142 MB)...", 2));

                    string? dir = Path.GetDirectoryName(_modelPath);
                    if (dir != null && !Directory.Exists(dir))
                        Directory.CreateDirectory(dir);

                    using var modelStream = await WhisperGgmlDownloader.GetGgmlModelAsync(GgmlType.Base);
                    using var fileWriter = File.OpenWrite(_modelPath);
                    await modelStream.CopyToAsync(fileWriter);

                    ModEntry.Logger?.Log("Modelo descargado correctamente.", LogLevel.Info);

                    StardewValley.Game1.addHUDMessage(new StardewValley.HUDMessage(
                        "Living Companions: Modelo de voz listo.", 1));
                }

                ModEntry.Logger?.Log("Inicializando Whisper...", LogLevel.Trace);
                _factory = WhisperFactory.FromPath(_modelPath);
                _isInitialized = true;
                ModEntry.Logger?.Log("Whisper inicializado correctamente.", LogLevel.Trace);
            }
            catch (Exception ex)
            {
                ModEntry.Logger?.Log($"Error al inicializar Whisper: {ex.Message}", LogLevel.Error);
            }
        }

        public async Task<string> TranscribeAudioAsync(float[] floatAudioBuffer)
        {
            if (!_isInitialized || _factory == null)
            {
                ModEntry.Logger?.Log("[Error] Whisper no está inicializado.", LogLevel.Error);
                return "[Error] Whisper no está inicializado.";
            }

            using var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(15));

            try
            {
                ModEntry.Logger?.Log($"Enviando {floatAudioBuffer.Length} muestras a Whisper...", LogLevel.Info);
                var stopwatch = System.Diagnostics.Stopwatch.StartNew();

                string finalTrimmedText = await Task.Run(async () =>
                {
                    ModEntry.Logger?.Log("Creando processor efímero...", LogLevel.Trace);

                    await using var processor = _factory.CreateBuilder()
                        .WithLanguage("es")
                        .WithNoContext()
                        .Build();

                    var sb = new System.Text.StringBuilder();

                    ModEntry.Logger?.Log("Iniciando ProcessAsync con Whisper.net...", LogLevel.Trace);

                    await foreach (var segment in processor.ProcessAsync(floatAudioBuffer, cts.Token))
                    {
                        sb.Append(segment.Text);
                        ModEntry.Logger?.Log($"Segmento: '{segment.Text}'", LogLevel.Trace);
                    }

                    return sb.ToString().Trim();

                }, cts.Token).ConfigureAwait(false);

                stopwatch.Stop();
                ModEntry.Logger?.Log($"Transcripción: '{finalTrimmedText}' en {stopwatch.ElapsedMilliseconds}ms.", LogLevel.Info);

                return finalTrimmedText;
            }
            catch (OperationCanceledException)
            {
                ModEntry.Logger?.Log("[Error] Timeout (15s) al transcribir.", LogLevel.Error);
                return "[Error] Tiempo de espera agotado.";
            }
            catch (AccessViolationException ex)
            {
                ModEntry.Logger?.Log($"[CRASH NATIVO] AccessViolation: {ex}", LogLevel.Error);
                return "[Error] Crash de memoria al transcribir.";
            }
            catch (System.Runtime.InteropServices.SEHException ex)
            {
                ModEntry.Logger?.Log($"[CRASH NATIVO] SEHException: {ex}", LogLevel.Error);
                return "[Error] Fallo interno del motor Whisper.";
            }
            catch (Exception ex)
            {
                ModEntry.Logger?.Log($"Error al transcribir: {ex.Message}\n{ex.StackTrace}", LogLevel.Error);
                return "[Error] Excepción en transcripción.";
            }
        }

        public void Dispose()
        {
            _factory?.Dispose();
        }
    }
}