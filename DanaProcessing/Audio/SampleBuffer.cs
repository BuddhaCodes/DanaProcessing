using System;
using System.IO;

namespace DanaProcessing
{
    /// <summary>
    /// A sound loaded into memory, ready for Sample(...). Always stored as two
    /// float channels (mono files share one array for both) at the file's own
    /// sample rate -- the engine resamples on playback, so a 44.1 kHz file
    /// plays at the right pitch on a 48 kHz device.
    /// </summary>
    public sealed class SampleBuffer
    {
        public float[] Left { get; }
        public float[] Right { get; }
        public int Frames { get; }
        public int SampleRate { get; }
        public int Channels { get; }
        public string Name { get; }

        /// <summary>Length in seconds at normal speed (rate: 1).</summary>
        public double Duration => Frames / (double)SampleRate;

        private SampleBuffer(string name, float[] left, float[] right, int sampleRate, int channels)
        {
            Name = name;
            Left = left;
            Right = right;
            Frames = left.Length;
            SampleRate = sampleRate;
            Channels = channels;
        }

        /// <summary>Wraps an existing mono signal (e.g. one you generated in code).</summary>
        public static SampleBuffer FromMono(float[] data, int sampleRate, string name = "buffer")
            => new(name, data, data, sampleRate, 1);

        /// <summary>
        /// Loads a .wav file: PCM 8/16/24/32-bit or float 32/64-bit, mono or
        /// stereo (extra channels beyond the first two are ignored). Plain
        /// managed parser -- no external decoder involved.
        /// </summary>
        public static SampleBuffer LoadWav(string path)
        {
            using var stream = File.OpenRead(path);
            using var reader = new BinaryReader(stream);

            if (new string(reader.ReadChars(4)) != "RIFF")
                throw new InvalidDataException($"LoadSample(): \"{path}\" no es un archivo WAV (falta RIFF).");
            reader.ReadUInt32();
            if (new string(reader.ReadChars(4)) != "WAVE")
                throw new InvalidDataException($"LoadSample(): \"{path}\" no es un archivo WAV (falta WAVE).");

            int format = 0, channels = 0, sampleRate = 0, bits = 0;
            byte[]? data = null;

            while (stream.Position + 8 <= stream.Length)
            {
                var id = new string(reader.ReadChars(4));
                var size = reader.ReadUInt32();
                long next = stream.Position + size + (size & 1);

                if (id == "fmt ")
                {
                    format = reader.ReadUInt16();
                    channels = reader.ReadUInt16();
                    sampleRate = (int)reader.ReadUInt32();
                    reader.ReadUInt32(); // byte rate
                    reader.ReadUInt16(); // block align
                    bits = reader.ReadUInt16();
                    if (format == 0xFFFE && size >= 40)
                    {
                        reader.ReadUInt16(); // cbSize
                        reader.ReadUInt16(); // valid bits
                        reader.ReadUInt32(); // channel mask
                        format = reader.ReadUInt16(); // first 2 bytes of the subformat GUID
                    }
                }
                else if (id == "data")
                {
                    long available = Math.Min(size, stream.Length - stream.Position);
                    data = reader.ReadBytes((int)available);
                }

                if (next > stream.Length)
                    break;
                stream.Position = next;
            }

            if (data == null || channels == 0 || sampleRate == 0)
                throw new InvalidDataException($"LoadSample(): \"{path}\" no tiene datos de audio válidos.");

            int bytesPerSample = bits / 8;
            if (bytesPerSample == 0)
                throw new InvalidDataException($"LoadSample(): \"{path}\" tiene {bits} bits por muestra, no soportado.");

            int frames = data.Length / (bytesPerSample * channels);
            var left = new float[frames];
            var right = channels > 1 ? new float[frames] : left;

            for (int f = 0; f < frames; f++)
            {
                int offset = f * bytesPerSample * channels;
                left[f] = ReadSample(data, offset, format, bits, path);
                if (channels > 1)
                    right[f] = ReadSample(data, offset + bytesPerSample, format, bits, path);
            }

            return new SampleBuffer(Path.GetFileNameWithoutExtension(path), left, right, sampleRate, Math.Min(channels, 2));
        }

        private static float ReadSample(byte[] d, int o, int format, int bits, string path)
        {
            if (format == 1)
            {
                return bits switch
                {
                    8 => (d[o] - 128) / 128f,
                    16 => BitConverter.ToInt16(d, o) / 32768f,
                    24 => ((d[o] | (d[o + 1] << 8) | ((sbyte)d[o + 2] << 16))) / 8388608f,
                    32 => BitConverter.ToInt32(d, o) / 2147483648f,
                    _ => throw new InvalidDataException($"LoadSample(): \"{path}\" usa PCM de {bits} bits, no soportado."),
                };
            }
            if (format == 3)
            {
                return bits switch
                {
                    32 => BitConverter.ToSingle(d, o),
                    64 => (float)BitConverter.ToDouble(d, o),
                    _ => throw new InvalidDataException($"LoadSample(): \"{path}\" usa float de {bits} bits, no soportado."),
                };
            }
            throw new InvalidDataException($"LoadSample(): \"{path}\" usa un formato WAV comprimido ({format}); exportalo como PCM o float.");
        }
    }
}
