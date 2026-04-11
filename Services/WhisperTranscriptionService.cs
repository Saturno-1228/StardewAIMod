using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using StardewModdingAPI;
using Whisper.net;

namespace StardewAIMod.Services
{
    public class WhisperTranscriptionService
    {
        private readonly string _modelPath;
        private readonly IMonitor _monitor;
        private WhisperFactory _whisperFactory;

        public WhisperTranscriptionService(string modelPath, IMonitor monitor)
        {
            _modelPath = modelPath;
            _monitor = monitor;

            InitializeWhisperAsync().ConfigureAwait(false);
        }

        private async Task InitializeWhisperAsync()
        {
            try
            {
                if (!File.Exists(_modelPath))
                {
                    _monitor.Log($"[Studio Corvus] ⚠️ Whisper model not found at {_modelPath}. Transcription will not work until a model is placed there.", LogLevel.Warn);
                    return;
                }

                _monitor.Log($"[Studio Corvus] ⚙️ Loading Whisper model from: {_modelPath}", LogLevel.Info);

                // Initialize WhisperFactory on a background thread to prevent blocking
                await Task.Run(() =>
                {
                    _whisperFactory = WhisperFactory.FromPath(_modelPath);
                });

                _monitor.Log("[Studio Corvus] ✅ Whisper model loaded successfully.", LogLevel.Info);
            }
            catch (Exception ex)
            {
                _monitor.Log($"[Studio Corvus] ❌ Failed to load Whisper model: {ex.Message}", LogLevel.Error);
            }
        }

        /// <summary>
        /// Transcribes 16kHz float PCM audio using local Whisper.
        /// </summary>
        /// <param name="pcmData">Array of 32-bit float PCM audio samples, at 16kHz.</param>
        public async Task<string> TranscribeAudioAsync(float[] pcmData)
        {
            if (_whisperFactory == null)
            {
                return "[Error: Whisper model not loaded or missing]";
            }

            if (pcmData == null || pcmData.Length == 0)
            {
                return string.Empty;
            }

            try
            {
                var sb = new StringBuilder();

                using var processor = _whisperFactory.CreateBuilder()
                    .WithLanguage("auto")
                    .Build();

                await foreach (var segment in processor.ProcessAsync(pcmData))
                {
                    sb.Append(segment.Text);
                }

                return sb.ToString().Trim();
            }
            catch (Exception ex)
            {
                _monitor.Log($"[Studio Corvus] ❌ Whisper Transcription Error: {ex.Message}", LogLevel.Error);
                return $"[Transcription Error: {ex.Message}]";
            }
        }
    }
}
