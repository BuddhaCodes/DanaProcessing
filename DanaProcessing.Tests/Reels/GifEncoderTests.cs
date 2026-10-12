using DanaProcessing.Reels;
using Xunit;

namespace DanaProcessing.Tests.Reels;

public class GifEncoderTests
{
    private const int W = 160, H = 90;

    private static byte[] Frame(int i)
    {
        var px = new byte[W * H * 4];
        for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
            {
                int o = (y * W + x) * 4;
                px[o] = (byte)(x * 255 / W);
                px[o + 1] = (byte)(y * 255 / H);
                px[o + 2] = (byte)(i < 6 ? 0 : 60);       // background changes once
                px[o + 3] = 255;
                if (x > (i / 2) * 10 && x < (i / 2) * 10 + 30 && y > 30 && y < 60)
                {
                    px[o] = 255; px[o + 1] = 176; px[o + 2] = 140;  // a moving box, still on odd frames
                }
            }
        return px;
    }

    [Fact]
    public void Round_trips_through_a_gif_decoder()
    {
        var frames = Enumerable.Range(0, 12).Select(Frame).ToList();
        var ms = new MemoryStream();
        using (var gif = new GifEncoder(ms, W, H))
            foreach (var f in frames)
                gif.AddFrame(f, 7);

        var decoded = MiniGifDecoder.Decode(ms.ToArray(), out int w, out int h, out var delays);
        Assert.Equal((W, H), (w, h));
        Assert.Equal(frames.Count, decoded.Count);
        Assert.All(delays, d => Assert.Equal(7, d));

        double sum = 0; int max = 0; long n = 0;
        for (int i = 0; i < frames.Count; i++)
            for (int k = 0; k < W * H * 4; k += 4)
                for (int c = 0; c < 3; c++)
                {
                    int e = Math.Abs(decoded[i][k + c] - frames[i][k + c]);
                    sum += e; n++; max = Math.Max(max, e);
                }
        Assert.True(sum / n < 6, $"mean error {sum / n:0.00}");
        Assert.True(max < 40, $"max error {max}");
    }

    [Fact]
    public void Unchanged_frames_cost_almost_nothing()
    {
        var still = Frame(0);
        var one = new MemoryStream();
        using (var gif = new GifEncoder(one, W, H)) gif.AddFrame(still, 5);
        var many = new MemoryStream();
        using (var gif = new GifEncoder(many, W, H)) for (int i = 0; i < 20; i++) gif.AddFrame(still, 5);
        // 19 identical frames are stored as tiny 1x1 updates with a 2-color table.
        Assert.True(many.Length - one.Length < 19 * 40, $"{many.Length - one.Length} bytes for 19 repeats");
    }
}

/// <summary>Just enough of a GIF89a decoder to check our encoder: local palettes, LZW, sub-rectangles, "leave in place" disposal.</summary>
internal static class MiniGifDecoder
{
    public static List<byte[]> Decode(byte[] data, out int width, out int height, out List<int> delays)
    {
        int p = 0;
        Assert.Equal("GIF89a", System.Text.Encoding.ASCII.GetString(data, 0, 6));
        p = 6;
        width = data[p] | data[p + 1] << 8;
        height = data[p + 2] | data[p + 3] << 8;
        byte flags = data[p + 4];
        p += 7;
        if ((flags & 0x80) != 0) p += 3 * (2 << (flags & 7));

        var canvas = new byte[width * height * 4];
        var frames = new List<byte[]>();
        delays = new List<int>();
        int delay = 0;

        while (p < data.Length)
        {
            byte b = data[p++];
            if (b == 0x3B) break;
            if (b == 0x21)
            {
                byte label = data[p++];
                if (label == 0xF9) delay = data[p + 2] | data[p + 3] << 8;
                while (data[p] != 0) p += data[p] + 1;
                p++;
                continue;
            }
            Assert.Equal(0x2C, b);
            int ix = data[p] | data[p + 1] << 8, iy = data[p + 2] | data[p + 3] << 8;
            int iw = data[p + 4] | data[p + 5] << 8, ih = data[p + 6] | data[p + 7] << 8;
            byte f = data[p + 8];
            p += 9;
            Assert.True((f & 0x80) != 0, "encoder always writes a local palette");
            int palSize = 2 << (f & 7);
            var pal = data.AsSpan(p, palSize * 3).ToArray();
            p += palSize * 3;
            int minCode = data[p++];
            var lzw = new List<byte>();
            while (data[p] != 0) { lzw.AddRange(data.AsSpan(p + 1, data[p]).ToArray()); p += data[p] + 1; }
            p++;

            var idx = Lzw(lzw.ToArray(), minCode, iw * ih);
            for (int y = 0; y < ih; y++)
                for (int x = 0; x < iw; x++)
                {
                    int ci = idx[y * iw + x];
                    int o = ((iy + y) * width + ix + x) * 4;
                    canvas[o] = pal[ci * 3]; canvas[o + 1] = pal[ci * 3 + 1]; canvas[o + 2] = pal[ci * 3 + 2]; canvas[o + 3] = 255;
                }
            frames.Add((byte[])canvas.Clone());
            delays.Add(delay);
        }
        return frames;
    }

    private static byte[] Lzw(byte[] input, int minCode, int count)
    {
        int clear = 1 << minCode, eoi = clear + 1;
        int size = minCode + 1, next = eoi + 1;
        var dict = new List<byte[]>();
        void Reset() { dict.Clear(); for (int i = 0; i < clear; i++) dict.Add(new[] { (byte)i }); dict.Add(Array.Empty<byte>()); dict.Add(Array.Empty<byte>()); size = minCode + 1; next = eoi + 1; }
        Reset();
        var output = new List<byte>(count);
        int bitPos = 0;
        byte[]? prev = null;
        while (output.Count < count)
        {
            int code = 0;
            for (int i = 0; i < size; i++, bitPos++)
                if ((input[bitPos >> 3] >> (bitPos & 7) & 1) != 0) code |= 1 << i;
            if (code == clear) { Reset(); prev = null; continue; }
            if (code == eoi) break;
            byte[] entry = code < dict.Count ? dict[code] : prev!.Concat(new[] { prev![0] }).ToArray();
            output.AddRange(entry);
            if (prev != null && next < 4096)
            {
                dict.Add(prev.Concat(new[] { entry[0] }).ToArray());
                next++;
                if (next == (1 << size) && size < 12) size++;
            }
            prev = entry;
        }
        return output.ToArray();
    }
}
