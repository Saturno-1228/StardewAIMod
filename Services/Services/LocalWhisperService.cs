using System;
using System.IO;
using System.Threading.Tasks;
using StardewModdingAPI;
using Whisper.net;
using Whisper.net.Ggml;

namespace StardewLivingValley.Services
{
    /// <summary>
    /// Servicio que envuelve la lógica asíncrona de Whisper.net para transcripción offline.
    /// Descarga automáticamente el modelo la primera vez que se necesita.
    /// </summary>
    public class LocalWhisperService
    {
        private WhisperFactory? _factory;
        private WhisperProcessor? _processor;
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

            try
            {
                // Si el modelo no existe, se descarga
                if (!File.Exists(_modelPath))
                {
                    ModEntry.Logger?.Log("El modelo Whisper no se encontró en la carpeta de Assets. Descargando ggml-base.bin (puede tardar un momento)...", LogLevel.Info);
                    
                    // Asegurarse de que el directorio exista
                    string? dir = Path.GetDirectoryName(_modelPath);
                    if (dir != null && !Directory.Exists(dir))
                    {
                        Directory.CreateDirectory(dir);
                    }

                    using var modelStream = await WhisperGgmlDownloader.GetGgmlModelAsync(GgmlType.Base);
                    using var fileWriter = File.OpenWrite(_modelPath);
                    await modelStream.CopyToAsync(fileWriter);
                    
                    ModEntry.Logger?.Log("Modelo descargado exitosamente.", LogLevel.Info);
                }

                ModEntry.Logger?.Log("Inicializando modelo de Whisper local...", LogLevel.Trace);
                _factory = WhisperFactory.FromPath(_modelPath);
                
                // Español por defecto si se desea, o "auto"
                _processor = _factory.CreateBuilder()
                    .WithLanguage("es")
                    .Build();

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
        public async Task<string> TranscribeAudioAsync(float[] floatAudioBuffer)
        {
            if (!_isInitialized || _processor == null)
            {
                return "[Error] Whisper no está inicializado.";
            }

            try
            {
                // Whisper.net espera los datos en un stream de floats o un array procesado
                string resultText = "";
                await foreach (var result in _processor.ProcessAsync(floatAudioBuffer))
                {
                    resultText += result.Text;
                }

                return resultText.Trim();
            }
            catch (Exception ex)
            {
                ModEntry.Logger?.Log($"Error al transcribir el audio: {ex.Message}", LogLevel.Error);
                return "[Error] Excepción en transcripción de Whisper.";
            }
        }

        public void Dispose()
        {
            _processor?.Dispose();
            _factory?.Dispose();
        }
    }
}
