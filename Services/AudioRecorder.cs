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

        public float[] StopRecording()
        {
            if (!_isRecording || _microphone == null) return null;

            _isRecording = false;
            _microphone.Stop();

            // Get raw PCM data
            byte[] pcmData = _audioStream.ToArray();
            _audioStream.Dispose();
            _audioStream = null;

            // Convert to 16kHz float[] for Whisper
            return ConvertTo16kHzFloat(pcmData, _microphone.SampleRate);
        }

        private float[] ConvertTo16kHzFloat(byte[] pcmData, int sampleRate)
        {
            int numSamples = pcmData.Length / 2; // 16-bit PCM = 2 bytes per sample
            float[] floatData = new float[numSamples];

            // Convert 16-bit PCM to 32-bit float [-1.0f, 1.0f]
            for (int i = 0; i < numSamples; i++)
            {
                short sample = BitConverter.ToInt16(pcmData, i * 2);
                floatData[i] = sample / 32768.0f;
            }

            // Resample to 16kHz if necessary
            if (sampleRate != 16000)
            {
                floatData = Resample(floatData, sampleRate, 16000);
            }

            return floatData;
        }

        private float[] Resample(float[] input, int inputSampleRate, int outputSampleRate)
        {
            if (inputSampleRate == outputSampleRate) return input;

            double ratio = (double)inputSampleRate / outputSampleRate;
            int outLength = (int)(input.Length / ratio);
            float[] output = new float[outLength];

            // Simple linear interpolation
            for (int i = 0; i < outLength; i++)
            {
                double inputIndex = i * ratio;
                int index1 = (int)Math.Floor(inputIndex);
                int index2 = Math.Min(index1 + 1, input.Length - 1);
                double fraction = inputIndex - index1;

                output[i] = (float)((1.0 - fraction) * input[index1] + fraction * input[index2]);
            }

            return output;
        }
    }
}
