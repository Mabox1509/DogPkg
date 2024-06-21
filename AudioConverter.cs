using System;
using System.IO;
using NAudio.Wave;
using NVorbis;

namespace DogPkg
{
    public class AudioConverter
    {
        public static void ConvertFileToWave(string filePath, out float[] leftChannel, out float[] rightChannel, out int sampleRate)
        {
            string extension = Path.GetExtension(filePath).ToLower();
            switch (extension)
            {
                case ".wav":
                    ConvertWavToFloatArray(filePath, out leftChannel, out rightChannel, out sampleRate);
                    break;
                case ".mp3":
                    throw new Exception("Invalid format");

                case ".ogg":
                    ConvertOggToFloatArray(filePath, out leftChannel, out rightChannel, out sampleRate);
                    break;
                default:
                    throw new NotSupportedException($"Unsupported file extension: {extension}");
            }
        }

        private static void ConvertWavToFloatArray(string filePath, out float[] leftChannel, out float[] rightChannel, out int sampleRate)
        {
            using (var reader = new AudioFileReader(filePath))
            {
                sampleRate = reader.WaveFormat.SampleRate;
                int channels = reader.WaveFormat.Channels;
                var samples = new float[reader.Length / sizeof(float)];
                reader.Read(samples, 0, samples.Length);

                leftChannel = new float[samples.Length / channels];
                rightChannel = channels == 2 ? new float[samples.Length / channels] : null;

                for (int i = 0; i < samples.Length; i += channels)
                {
                    leftChannel[i / channels] = samples[i];
                    if (channels == 2)
                    {
                        rightChannel[i / channels] = samples[i + 1];
                    }
                }
            }
        }

        private static void ConvertOggToFloatArray(string filePath, out float[] leftChannel, out float[] rightChannel, out int sampleRate)
        {
            using (var vorbis = new VorbisReader(filePath))
            {
                sampleRate = vorbis.SampleRate;
                int channels = vorbis.Channels;
                long totalSamples = vorbis.TotalSamples;

                leftChannel = new float[totalSamples / channels];
                rightChannel = channels == 2 ? new float[totalSamples / channels] : null;

                int sampleIndex = 0;
                float[] buffer = new float[vorbis.Channels * 4096];
                while (vorbis.ReadSamples(buffer, 0, buffer.Length) > 0)
                {
                    for (int i = 0; i < buffer.Length; i += channels)
                    {
                        leftChannel[sampleIndex] = buffer[i];
                        if (channels == 2)
                        {
                            rightChannel[sampleIndex] = buffer[i + 1];
                        }
                        sampleIndex++;
                    }
                }
            }
        }
    }

}
