using System;
using System.IO;
using System.Runtime.InteropServices;
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
        private readonly string _modDirectory;
        private bool _isInitialized;
        
        // Punteros para liberar la memoria después
        private IntPtr _ggmlHandle = IntPtr.Zero;
        private IntPtr _whisperHandle = IntPtr.Zero;

        public LocalWhisperService(IModHelper helper)
        {
            _modDirectory = helper.DirectoryPath;
            _modelPath = Path.Combine(_modDirectory, "Assets", "ggml-base.bin");

            AppDomain.CurrentDomain.UnhandledException += (sender, e) =>
            {
                ModEntry.Logger?.Log($"[CRASH NATIVO WHISPER] {e.ExceptionObject}", LogLevel.Error);
            };

            // Inyectamos la ruta en el PATH global de Windows para que las DLLs se vean entre sí
            InjectDirectoryIntoPath();
        }

        private void InjectDirectoryIntoPath()
        {
            try
            {
                string pathVar = Environment.GetEnvironmentVariable("PATH") ?? "";
                if (!pathVar.Contains(_modDirectory))
                {
                    string newPath = _modDirectory + Path.PathSeparator + pathVar;
                    Environment.SetEnvironmentVariable("PATH", newPath);
                    ModEntry.Logger?.Log($"[Victoria] Directorio del mod forzado en la variable PATH del sistema operativo.", LogLevel.Trace);
                }
            }
            catch (Exception ex)
            {
                ModEntry.Logger?.Log($"Error al modificar el PATH: {ex.Message}", LogLevel.Error);
            }
        }

        /// <summary>
        /// Carga las librerías a la fuerza en la RAM del juego antes de que Whisper las pida.
        /// </summary>
        private void ForceLoadNativeLibrariesToRam()
        {
            string ggmlPath = Path.Combine(_modDirectory, "ggml-whisper.dll");
            string whisperPath = Path.Combine(_modDirectory, "whisper.dll");

            ModEntry.Logger?.Log("Iniciando inyección manual de librerías nativas en la RAM...", LogLevel.Info);

            // 1. OBLIGATORIO: Cargar GGML primero (dependencia matemática)
            if (File.Exists(ggmlPath))
            {
                try
                {
                    _ggmlHandle = NativeLibrary.Load(ggmlPath);
                    ModEntry.Logger?.Log("-> ggml-whisper.dll inyectado en RAM exitosamente.", LogLevel.Info);
                }
                catch (Exception ex)
                {
                    ModEntry.Logger?.Log($"-> FALLO al inyectar ggml-whisper.dll: {ex.Message}", LogLevel.Error);
                }
            }
            else
            {
                ModEntry.Logger?.Log($"-> ERROR CRÍTICO: No se encontró físicamente el archivo {ggmlPath}", LogLevel.Error);
            }

            // 2. Cargar Whisper (depende de GGML)
            if (File.Exists(whisperPath))
            {
                try
                {
                    _whisperHandle = NativeLibrary.Load(whisperPath);
                    ModEntry.Logger?.Log("-> whisper.dll inyectado en RAM exitosamente.", LogLevel.Info);
                }
                catch (Exception ex)
                {
                    ModEntry.Logger?.Log($"-> FALLO al inyectar whisper.dll: {ex.Message}", LogLevel.Error);
                }
            }
            else
            {
                ModEntry.Logger?.Log($"-> ERROR CRÍTICO: No se encontró físicamente el archivo {whisperPath}", LogLevel.Error);
            }
        }

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

                    using var modelStream = await WhisperGgmlDownloader.GetGgmlModelAsync(GgmlType.Base);
                    using var fileWriter = File.OpenWrite(_modelPath);
                    await modelStream.CopyToAsync(fileWriter);

                    ModEntry.Logger?.Log("Modelo descargado exitosamente.", LogLevel.Info);
                }

                // INYECCIÓN DE FUERZA BRUTA EN RAM ANTES DE LLAMAR A LA FÁBRICA
                ForceLoadNativeLibrariesToRam();

                ModEntry.Logger?.Log("Inicializando modelo de Whisper local (Factory)...", LogLevel.Trace);
                
                _factory = WhisperFactory.FromPath(_modelPath);
                
                _isInitialized = true;
                ModEntry.Logger?.Log("Whisper inicializado correctamente. ¡Hemos triunfado!", LogLevel.Info);
            }
            catch (Exception ex)
            {
                ModEntry.Logger?.Log($"Error crítico al inicializar Whisper.net: {ex.Message}\nStack: {ex.StackTrace}", LogLevel.Error);
            }
        }

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
                    await using var processor = _factory.CreateBuilder()
                        .WithLanguage("es")
                        .WithNoContext()
                        .Build();

                    var sb = new System.Text.StringBuilder();

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
            catch (Exception ex)
            {
                ModEntry.Logger?.Log($"Error al transcribir: {ex.Message}\n{ex.StackTrace}", LogLevel.Error);
                return "[Error] Excepción en transcripción de Whisper.";
            }
        }

        public void Dispose()
        {
            _factory?.Dispose();
            if (_whisperHandle != IntPtr.Zero) NativeLibrary.Free(_whisperHandle);
            if (_ggmlHandle != IntPtr.Zero) NativeLibrary.Free(_ggmlHandle);
        }
    }
}