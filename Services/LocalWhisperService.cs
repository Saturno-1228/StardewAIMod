using System;
using System.IO;
using System.Reflection;
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
        private static bool _resolverRegistered = false;

        public LocalWhisperService(IModHelper helper)
        {
            _modDirectory = helper.DirectoryPath;
            _modelPath = Path.Combine(_modDirectory, "Assets", "ggml-base.bin");

            AppDomain.CurrentDomain.UnhandledException += (sender, e) =>
            {
                ModEntry.Logger?.Log($"[CRASH NATIVO WHISPER] {e.ExceptionObject}", LogLevel.Error);
            };

            // 1. LA FUERZA BRUTA: METODOLOGÍA B (Inyección de Entorno)
            // Esto soluciona que "whisper.dll" (C++) no encuentre a "ggml-whisper.dll" (C++)
            InjectDirectoryIntoPath();

            // 2. EL BISTURÍ: METODOLOGÍA C (Interceptor de C# a C++)
            // Esto soluciona que el contexto de SMAPI no encuentre "whisper.dll"
            if (!_resolverRegistered)
            {
                NativeLibrary.SetDllImportResolver(typeof(WhisperFactory).Assembly, WhisperDllImportResolver);
                _resolverRegistered = true;
                ModEntry.Logger?.Log("DllImportResolver de .NET registrado con éxito.", LogLevel.Info);
            }
        }

        private void InjectDirectoryIntoPath()
        {
            try
            {
                string pathVar = Environment.GetEnvironmentVariable("PATH") ?? "";
                
                // Si la ruta del mod no está en el PATH de Windows, la forzamos al PRINCIPIO
                if (!pathVar.Contains(_modDirectory))
                {
                    string newPath = _modDirectory + Path.PathSeparator + pathVar;
                    Environment.SetEnvironmentVariable("PATH", newPath);
                    ModEntry.Logger?.Log($"[Victoria] Directorio del mod forzado en la variable PATH del sistema operativo.", LogLevel.Info);
                }
            }
            catch (Exception ex)
            {
                ModEntry.Logger?.Log($"Error al modificar el PATH: {ex.Message}", LogLevel.Error);
            }
        }

        private IntPtr WhisperDllImportResolver(string libraryName, Assembly assembly, DllImportSearchPath? searchPath)
        {
            // Atrapamos cualquier petición que contenga "whisper"
            if (libraryName.Contains("whisper", StringComparison.OrdinalIgnoreCase))
            {
                string absoluteWhisperPath = Path.Combine(_modDirectory, "whisper.dll");
                string absoluteGgmlPath = Path.Combine(_modDirectory, "ggml-whisper.dll");

                try
                {
                    // 1. Obligamos a cargar la dependencia matemática (GGML) en la memoria primero
                    if (File.Exists(absoluteGgmlPath))
                    {
                        ModEntry.Logger?.Log("Interceptor: Precargando ggml-whisper.dll en memoria...", LogLevel.Trace);
                        NativeLibrary.TryLoad(absoluteGgmlPath, out _); 
                    }

                    // 2. Entregamos el motor base (whisper.dll) de forma absoluta
                    if (File.Exists(absoluteWhisperPath))
                    {
                        ModEntry.Logger?.Log($"Interceptor: Forzando carga absoluta de {absoluteWhisperPath}...", LogLevel.Trace);
                        if (NativeLibrary.TryLoad(absoluteWhisperPath, out IntPtr handle))
                        {
                            return handle;
                        }
                    }
                }
                catch (Exception ex)
                {
                    ModEntry.Logger?.Log($"Error interno en el DllImportResolver: {ex.Message}", LogLevel.Error);
                }
            }
            return IntPtr.Zero;
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

                ModEntry.Logger?.Log("Inicializando modelo de Whisper local (Factory)...", LogLevel.Trace);
                
                // ¡AQUÍ ES LA HORA DE LA VERDAD!
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
        }
    }
}