using DanaProcessing.Tests.Support;
using SkiaSharp;
using Xunit;

namespace DanaProcessing.Tests.Core;

/// <summary>Seeds: the same seed draws the same thing, and every PNG a sketch saves remembers how to make it again.</summary>
public class SeedTests
{
    public class Scatter : Sketch
    {
        [Tweak(1, 20)] public float Dot = 6;
        [Tweak] public Color Ink = new Color(250, 120, 40);
        public List<float> Values = new();
        public override void Setup()
        {
            Size(200, 120);
            for (int i = 0; i < 5; i++)
                Values.Add(Random(1000));
            Values.Add(RandomGaussian());
            Values.Add(Noise(3.3f, 1.7f));
        }
        public override void Draw()
        {
            Background(255);
            Fill(Ink);
            for (int i = 0; i < 50; i++)
                Circle(Random(Width), Random(Height), Dot);
        }
    }

    private static byte[] Pixels(SKSurface surface)
    {
        using var image = surface.Snapshot();
        using var bitmap = SKBitmap.FromImage(image);
        return bitmap.Bytes;
    }

    [Fact]
    public void The_same_seed_draws_the_same_picture()
    {
        var a = new Scatter();
        a.ApplySeed(1234);
        var pa = Pixels(Offscreen.Run(a, 3));

        var b = new Scatter();
        b.ApplySeed(1234);
        var pb = Pixels(Offscreen.Run(b, 3));

        Assert.Equal(a.Values, b.Values);   // Random, RandomGaussian and Noise
        Assert.Equal(pa, pb);

        var c = new Scatter();
        c.ApplySeed(999);
        Offscreen.Run(c, 1);
        Assert.NotEqual(a.Values.Take(5), c.Values.Take(5));
    }

    [Fact]
    public void Every_sketch_has_its_own_generator_and_a_seed_of_its_own()
    {
        var a = new Scatter();
        a.ApplySeed(7);
        var b = new Scatter();
        b.ApplySeed(7);
        float first = a.Random(1);
        b.Random(1); b.Random(1); b.Random(1);   // b drawing more must not move a
        var fresh = new Scatter();
        fresh.ApplySeed(7);
        Assert.Equal(fresh.Random(1), first);

        var unseeded = new Scatter();
        Assert.InRange(unseeded.Seed, 1, 999_999); // picks one the first time it's needed...
        Assert.Equal(unseeded.Seed, unseeded.Seed); // ...and keeps it
    }

    [Fact]
    public void RandomSeed_becomes_the_sketchs_seed()
    {
        var s = new Scatter();
        s.RandomSeed(42);
        Assert.Equal(42, s.Seed);
        float x = s.Random(1);
        s.RandomSeed(42);
        Assert.Equal(x, s.Random(1));
    }

    [Fact]
    public void Saved_pngs_remember_seed_sketch_frame_and_tweaks()
    {
        var path = Path.Combine(Path.GetTempPath(), $"dana_seed_{Guid.NewGuid():N}.png");
        try
        {
            var s = new Scatter();
            s.ApplySeed(4321);
            Offscreen.Run(s, 2);
            s.Dot = 12.5f;
            s.Save(path);

            var info = SketchImageInfo.Read(path);
            Assert.NotNull(info);
            Assert.Equal(4321, info!.Seed);
            Assert.Equal(nameof(Scatter), info.Sketch);
            Assert.Equal(2, info.Frame);
            Assert.Equal("12.5f", info.Tweaks["Dot"]);
            Assert.Equal("new Color(250, 120, 40)", info.Tweaks["Ink"]);

            // Still a perfectly normal PNG.
            using var decoded = SKBitmap.Decode(path);
            Assert.Equal(200, decoded.Width);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Png_chunks_have_valid_crcs_and_round_trip_unicode()
    {
        using var surface = Offscreen.NewSurface(4, 4);
        using var image = surface.Snapshot();
        var png = image.Encode(SKEncodedImageFormat.Png, 100).ToArray();

        var tagged = PngText.Add(png, new[] { new KeyValuePair<string, string>("Comment", "ñandú ✓") });
        Assert.Equal("ñandú ✓", PngText.Read(tagged)["Comment"]);

        // Walk every chunk and check its CRC.
        int pos = 8;
        while (pos < tagged.Length)
        {
            int length = System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(tagged.AsSpan(pos));
            uint expected = System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(tagged.AsSpan(pos + 8 + length));
            uint actual = PngText.Crc32(tagged.AsSpan(pos + 4, 4 + length), 0xFFFFFFFFu) ^ 0xFFFFFFFFu;
            Assert.Equal(expected, actual);
            pos += 12 + length;
        }
        Assert.Null(SketchImageInfo.Read(png)); // a plain PNG has no sketch info
    }

    [Fact]
    public void Saving_over_a_bigger_file_leaves_no_tail()
    {
        var path = Path.Combine(Path.GetTempPath(), $"dana_trunc_{Guid.NewGuid():N}.png");
        try
        {
            File.WriteAllBytes(path, new byte[2_000_000]);
            var s = new Scatter();
            Offscreen.Run(s, 1);
            s.Save(path);
            Assert.True(new FileInfo(path).Length < 200_000);
            using var decoded = SKBitmap.Decode(path);
            Assert.NotNull(decoded);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Tweak_values_read_from_a_png_go_back_into_the_fields()
    {
        var s = new Scatter();
        Offscreen.Run(s, 1);
        int applied = s.ApplyTweakLiterals(new Dictionary<string, string>
        {
            ["Dot"] = "3.25f",
            ["Ink"] = "new Color(1, 2, 3)",
            ["Missing"] = "5",
        });
        Assert.Equal(2, applied);
        Assert.Equal(3.25f, s.Dot);
        Assert.Equal(new Color(1, 2, 3), s.Ink);
        Assert.All(s.GetTweakValues(), v => Assert.True(v.Touched)); // so "Apply tweaks to code" writes them
    }
}
