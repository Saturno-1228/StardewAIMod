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

            using var modelStream = await WhisperGgmlDownloader.Default
                .GetGgmlModelAsync(GgmlType.Base);
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