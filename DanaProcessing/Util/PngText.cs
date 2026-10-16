using System.Buffers.Binary;
using System.Text;

namespace DanaProcessing
{
    /// <summary>
    /// Reads and writes PNG text chunks (iTXt, UTF-8): what DanaProcessing
    /// stores inside every PNG a sketch saves -- the seed, the sketch's class,
    /// the frame and the [Tweak] values -- so any image can be reproduced.
    /// Image viewers ignore the chunks; exiftool and similar tools show them.
    /// </summary>
    public static class PngText
    {
        private static readonly byte[] Signature = { 137, 80, 78, 71, 13, 10, 26, 10 };

        /// <summary>
        /// Returns <paramref name="png"/> with an iTXt chunk per entry added right
        /// after the header (IHDR). The pixels are untouched.
        /// </summary>
        public static byte[] Add(byte[] png, IEnumerable<KeyValuePair<string, string>> entries)
        {
            if (!IsPng(png))
                throw new ArgumentException("Not a PNG file.", nameof(png));

            // IHDR is always the first chunk: 8 (signature) + 4 (length) + 4 (type) + 13 (data) + 4 (CRC).
            int afterHeader = 8 + 12 + BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(8));
            using var output = new MemoryStream(png.Length + 256);
            output.Write(png, 0, afterHeader);
            foreach (var (key, value) in entries)
                WriteChunk(output, "iTXt", ITxtData(key, value));
            output.Write(png, afterHeader, png.Length - afterHeader);
            return output.ToArray();
        }

        /// <summary>Every tEXt / iTXt entry in <paramref name="png"/>, by keyword (empty for anything that isn't a PNG).</summary>
        public static IReadOnlyDictionary<string, string> Read(byte[] png)
        {
            var result = new Dictionary<string, string>();
            if (!IsPng(png))
                return result;

            int pos = 8;
            while (pos + 12 <= png.Length)
            {
                int length = BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(pos));
                if (length < 0 || pos + 12 + length > png.Length)
                    break;
                string type = Encoding.ASCII.GetString(png, pos + 4, 4);
                var data = png.AsSpan(pos + 8, length);

                if (type == "tEXt")
                {
                    int zero = data.IndexOf((byte)0);
                    if (zero > 0)
                        result[Encoding.Latin1.GetString(data[..zero])] = Encoding.Latin1.GetString(data[(zero + 1)..]);
                }
                else if (type == "iTXt")
                {
                    // keyword \0 compressionFlag compressionMethod languageTag \0 translatedKeyword \0 text
                    int k = data.IndexOf((byte)0);
                    if (k > 0 && k + 3 <= data.Length && data[k + 1] == 0)
                    {
                        var rest = data[(k + 3)..];
                        int lang = rest.IndexOf((byte)0);
                        if (lang >= 0)
                        {
                            var afterLang = rest[(lang + 1)..];
                            int translated = afterLang.IndexOf((byte)0);
                            if (translated >= 0)
                                result[Encoding.Latin1.GetString(data[..k])] = Encoding.UTF8.GetString(afterLang[(translated + 1)..]);
                        }
                    }
                }
                else if (type == "IEND")
                {
                    break;
                }
                pos += 12 + length;
            }
            return result;
        }

        private static bool IsPng(byte[] bytes) => bytes.Length > 33 && bytes.AsSpan(0, 8).SequenceEqual(Signature);

        internal static byte[] ITxtData(string key, string value)
        {
            // Keywords are 1-79 Latin-1 characters.
            string keyword = new string(key.Where(c => c >= 32 && c <= 255).Take(79).ToArray());
            if (keyword.Length == 0)
                keyword = "Comment";
            using var data = new MemoryStream();
            data.Write(Encoding.Latin1.GetBytes(keyword));
            data.Write(new byte[] { 0, 0, 0 }); // \0, not compressed, method 0
            data.WriteByte(0);                   // empty language tag \0
            data.WriteByte(0);                   // empty translated keyword \0
            data.Write(Encoding.UTF8.GetBytes(value));
            return data.ToArray();
        }

        internal static void WriteChunk(Stream output, string type, ReadOnlySpan<byte> data)
        {
            Span<byte> header = stackalloc byte[8];
            BinaryPrimitives.WriteInt32BigEndian(header, data.Length);
            Encoding.ASCII.GetBytes(type, header[4..]);
            output.Write(header);
            output.Write(data);

            uint crc = Crc32(header[4..], 0xFFFFFFFFu);
            crc = Crc32(data, crc) ^ 0xFFFFFFFFu;
            Span<byte> crcBytes = stackalloc byte[4];
            BinaryPrimitives.WriteUInt32BigEndian(crcBytes, crc);
            output.Write(crcBytes);
        }

        private static readonly uint[] CrcTable = BuildCrcTable();

        private static uint[] BuildCrcTable()
        {
            var table = new uint[256];
            for (uint n = 0; n < 256; n++)
            {
                uint c = n;
                for (int k = 0; k < 8; k++)
                    c = (c & 1) != 0 ? 0xEDB88320u ^ (c >> 1) : c >> 1;
                table[n] = c;
            }
            return table;
        }

        /// <summary>PNG's CRC-32, continuing from <paramref name="crc"/> (start with 0xFFFFFFFF, xor the end with 0xFFFFFFFF).</summary>
        internal static uint Crc32(ReadOnlySpan<byte> bytes, uint crc)
        {
            foreach (byte b in bytes)
                crc = CrcTable[(crc ^ b) & 0xFF] ^ (crc >> 8);
            return crc;
        }
    }

    /// <summary>
    /// What DanaProcessing wrote inside a PNG it saved: enough to make the same
    /// image again. Read it with <see cref="Read(string)"/>; the IDE's "Open
    /// seed from PNG..." does exactly that.
    /// </summary>
    public sealed record SketchImageInfo(int? Seed, string? Sketch, int? Frame, IReadOnlyDictionary<string, string> Tweaks)
    {
        public const string SeedKey = "DanaProcessing.Seed";
        public const string SketchKey = "DanaProcessing.Sketch";
        public const string FrameKey = "DanaProcessing.Frame";
        public const string TweaksKey = "DanaProcessing.Tweaks";

        /// <summary>Reads the info from a PNG file; null if it has none (not saved by DanaProcessing, or not a PNG).</summary>
        public static SketchImageInfo? Read(string path) => Read(File.ReadAllBytes(path));

        public static SketchImageInfo? Read(byte[] png)
        {
            var text = PngText.Read(png);
            if (!text.ContainsKey(SeedKey) && !text.ContainsKey(SketchKey))
                return null;

            int? seed = text.TryGetValue(SeedKey, out var s) && int.TryParse(s, out var si) ? si : null;
            int? frame = text.TryGetValue(FrameKey, out var f) && int.TryParse(f, out var fi) ? fi : null;
            text.TryGetValue(SketchKey, out var sketch);

            var tweaks = new Dictionary<string, string>();
            if (text.TryGetValue(TweaksKey, out var t))
            {
                // One "field = C# literal" per line.
                foreach (var line in t.Split('\n'))
                {
                    int eq = line.IndexOf('=');
                    if (eq > 0)
                        tweaks[line[..eq].Trim()] = line[(eq + 1)..].Trim();
                }
            }
            return new SketchImageInfo(seed, sketch, frame, tweaks);
        }

        /// <summary>The chunks to write for <paramref name="sketch"/> right now.</summary>
        internal static IEnumerable<KeyValuePair<string, string>> Describe(Sketch sketch)
        {
            yield return new("Software", "DanaProcessing");
            yield return new(SeedKey, sketch.Seed.ToString(System.Globalization.CultureInfo.InvariantCulture));
            yield return new(SketchKey, sketch.GetType().Name);
            yield return new(FrameKey, sketch.FrameCount.ToString(System.Globalization.CultureInfo.InvariantCulture));
            var tweaks = sketch.GetTweakValues();
            if (tweaks.Count > 0)
                yield return new(TweaksKey, string.Join("\n", tweaks.Select(v => $"{v.FieldName} = {v.CSharpLiteral}")));
        }
    }
}
