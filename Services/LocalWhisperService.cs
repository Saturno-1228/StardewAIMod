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

        // --- IMPORTACIONES DE KERNEL32 (METODOLOGÍA A) ---
        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern bool SetDefaultDllDirectories(uint DirectoryFlags);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr AddDllDirectory(string lpPathName);

        private const uint LOAD_LIBRARY_SEARCH_DEFAULT_DIRS = 0x00001000;
        private const uint LOAD_LIBRARY_SEARCH_USER_DIRS = 0x00000400;

        public LocalWhisperService(IModHelper helper)
        {
            _modDirectory = helper.DirectoryPath;
            _modelPath = Path.Combine(_modDirectory, "Assets", "ggml-base.bin");

            AppDomain.CurrentDomain.UnhandledException += (sender, e) =>
            {
                ModEntry.Logger?.Log($"[CRASH NATIVO WHISPER] {e.ExceptionObject}", LogLevel.Error);
            };

            // INYECCIÓN DIRECTA AL SISTEMA OPERATIVO ANTES DE QUE WHISPER RESPIRE
            InjectNativeDirectory();
        }

        private void InjectNativeDirectory()
        {
            try
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    ModEntry.Logger?.Log("Interviniendo Kernel32 para inyectar el directorio del mod en el PATH dinámico de Windows...", LogLevel.Trace);
                    
                    // Inicializar banderas de seguridad requeridas por Windows
                    SetDefaultDllDirectories(LOAD_LIBRARY_SEARCH_DEFAULT_DIRS | LOAD_LIBRARY_SEARCH_USER_DIRS);

                    // Añadir la raíz de nuestro mod al buscador del sistema
                    IntPtr cookie = AddDllDirectory(_modDirectory);

                    if (cookie == IntPtr.Zero)
                    {
                        int errorCode = Marshal.GetLastWin32Error();
                        ModEntry.Logger?.Log($"[ADVERTENCIA] AddDllDirectory devolvió 0. Código de error Win32: {errorCode}", LogLevel.Warn);
                    }
                    else
                    {
                        ModEntry.Logger?.Log($"Directorio nativo inyectado con éxito en Kernel32: {_modDirectory}", LogLevel.Info);
                    }
                }
            }
            catch (Exception ex)
            {
                ModEntry.Logger?.Log($"Error al intentar inyectar directorios nativos: {ex.Message}", LogLevel.Error);
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

                ModEntry.Logger?.Log("Inicializando modelo de Whisper local (Factory)...", LogLevel.Trace);
                
                // Con el directorio inyectado en Windows, el NativeLibraryLoader interno de Whisper
                // y el cargador de PE de Windows encontrarán la DLL y sus dependencias sin problema.
                _factory = WhisperFactory.FromPath(_modelPath);
                
                _isInitialized = true;
                ModEntry.Logger?.Log("Whisper inicializado correctamente.", LogLevel.Info);
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