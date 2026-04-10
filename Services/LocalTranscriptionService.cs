using System;
using System.IO;
using System.Threading.Tasks;
using StardewModdingAPI;
using Whisper.net;

namespace StardewAIMod.Services
{
    public class LocalTranscriptionService
    {
        private readonly string _modelPath;
        private readonly IMonitor _monitor;
        private WhisperFactory _whisperFactory;
        private bool _isInitialized = false;

        public LocalTranscriptionService(string modelPath, IMonitor monitor)
        {
            _modelPath = modelPath;
            _monitor = monitor;
            Initialize();
        }

        private void Initialize()
        {
            if (!File.Exists(_modelPath))
            {
                _monitor.Log($"[LocalTranscription] ❌ Model not found at: {_modelPath}. Voice transcription will be disabled.", LogLevel.Error);
                return;
            }

            try
            {
                _whisperFactory = WhisperFactory.FromPath(_modelPath);
                _isInitialized = true;
                _monitor.Log("[LocalTranscription] ✅ Whisper.net model loaded successfully.", LogLevel.Info);
            }
            catch (Exception ex)
            {
                _monitor.Log($"[LocalTranscription] ❌ Error initializing Whisper.net: {ex.Message}", LogLevel.Error);
            }
        }

        public async Task<string> TranscribeAsync(byte[] wavData)
        {
            if (!_isInitialized)
            {
                return "[Error: Local transcription is not initialized (missing model)]";
            }

            if (wavData == null || wavData.Length == 0)
            {
                return string.Empty;
            }

            try
            {
                using var processor = _whisperFactory.CreateBuilder()
                    .WithLanguage("auto")
                    .Build();

                // Whisper.net requires a Stream of WAV data (or PCM directly, but we have a WAV structure from AudioRecorder)
                using var stream = new MemoryStream(wavData);

                var resultText = new System.Text.StringBuilder();

                // ProcessAsync reads from a WAV stream assuming it has correct RIFF/WAVE headers.
                // Our VoiceInteractionManager already checks and prepares the WAV.
                await foreach (var result in processor.ProcessAsync(stream))
                {
                    resultText.Append(result.Text).Append(" ");
                }

                string transcribedText = resultText.ToString().Trim();

                if (string.IsNullOrWhiteSpace(transcribedText))
                {
                    return "[No transcription output]";
                }

                return transcribedText;
            }
            catch (Exception ex)
            {
                return $"[Transcription Error: {ex.Message}]";
            }
        }
    }
}
