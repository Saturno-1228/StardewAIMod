using System;
using System.IO;
using System.Collections.Generic;
using Microsoft.Xna.Framework.Audio;

namespace StardewAIMod.Services
{
    public class AudioRecorder
    {
        private Microphone _microphone;
        private bool _isRecording = false;
        private MemoryStream _audioStream;
        private byte[] _buffer;

        public AudioRecorder()
        {
            if (Microphone.Default != null)
            {
                _microphone = Microphone.Default;
                _microphone.BufferReady += Microphone_BufferReady;
            }
        }

        public bool IsAvailable => _microphone != null;
        public bool IsRecording => _isRecording;

        public void StartRecording()
        {
            if (_microphone == null || _isRecording) return;

            _audioStream = new MemoryStream();
            _microphone.BufferDuration = TimeSpan.FromMilliseconds(100);
            _buffer = new byte[_microphone.GetSampleSizeInBytes(_microphone.BufferDuration)];
            _isRecording = true;
            _microphone.Start();
        }

        private void Microphone_BufferReady(object sender, EventArgs e)
        {
            if (!_isRecording || _microphone == null) return;

            int bytesRead = _microphone.GetData(_buffer);
            if (bytesRead > 0)
            {
                _audioStream.Write(_buffer, 0, bytesRead);
            }
        }

        public void Update() { }

        public byte[] StopRecording()
        {
            if (!_isRecording || _microphone == null) return null;

            _isRecording = false;
            _microphone.Stop();

            // Get raw PCM data
            byte[] pcmData = _audioStream.ToArray();
            _audioStream.Dispose();
            _audioStream = null;

            // Build WAV headers
            return BuildWavFile(pcmData, _microphone.SampleRate, 1, 16);
        }

        private byte[] BuildWavFile(byte[] pcmData, int sampleRate, int channels, int bitsPerSample)
        {
            using (var memStream = new MemoryStream())
            using (var writer = new BinaryWriter(memStream))
            {
                int byteRate = sampleRate * channels * (bitsPerSample / 8);
                int blockAlign = channels * (bitsPerSample / 8);

                // RIFF chunk
                writer.Write(new char[] { 'R', 'I', 'F', 'F' });
                writer.Write(36 + pcmData.Length); // ChunkSize
                writer.Write(new char[] { 'W', 'A', 'V', 'E' });

                // fmt chunk
                writer.Write(new char[] { 'f', 'm', 't', ' ' });
                writer.Write(16); // Subchunk1Size for PCM
                writer.Write((short)1); // AudioFormat (1 = PCM)
                writer.Write((short)channels);
                writer.Write(sampleRate);
                writer.Write(byteRate);
                writer.Write((short)blockAlign);
                writer.Write((short)bitsPerSample);

                // data chunk
                writer.Write(new char[] { 'd', 'a', 't', 'a' });
                writer.Write(pcmData.Length);
                writer.Write(pcmData);

                return memStream.ToArray();
            }
        }
    }
}
