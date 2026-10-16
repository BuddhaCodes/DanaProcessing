using DanaProcessing.Audio;
using SkiaSharp;
using Xunit;

namespace DanaProcessing.Tests.Core;

/// <summary>The variations sheet: plans, deterministic thumbnails, and preview mode.</summary>
public class VariationTests
{
    public class Field : Sketch
    {
        [Tweak(2, 40)] public float Size = 10;
        [Tweak(1, 50)] public int Count = 20;
        [Tweak] public bool Outline;
        public int CountSeenBySetup;

        public override void Setup()
        {
            Size(160, 100);
            CountSeenBySetup = Count;
        }

        public override void Draw()
        {
            Background(250);
            Fill(30, 60, 200);
            for (int i = 0; i < Count; i++)
                Circle(Random(Width), Noise(i * 0.3f, Millis() * 0.001f) * Height, Size);
        }
    }

    private static Sketch NewField() => new Field();

    private static byte[] Bytes(SKImage image)
    {
        using var bitmap = SKBitmap.FromImage(image);
        return bitmap.Bytes;
    }

    [Fact]
    public void Seeds_mode_varies_only_the_seed()
    {
        var plan = VariationRenderer.Plan(NewField, 9, VariationMode.Seeds, baseSeed: 500);
        Assert.Equal(9, plan.Count);
        Assert.Equal(500, plan[0].Seed);                            // cell 0 = what you have now
        Assert.Equal(9, plan.Select(p => p.Seed).Distinct().Count());
        Assert.All(plan, p => Assert.Empty(p.Tweaks));
    }

    [Fact]
    public void Tweaks_mode_keeps_the_seed_and_stays_inside_the_ranges()
    {
        var plan = VariationRenderer.Plan(NewField, 16, VariationMode.Tweaks, baseSeed: 77);
        Assert.All(plan, p => Assert.Equal(77, p.Seed));
        foreach (var (_, tweaks) in plan.Skip(1))
        {
            float size = float.Parse(tweaks["Size"].TrimEnd('f'), System.Globalization.CultureInfo.InvariantCulture);
            int count = int.Parse(tweaks["Count"]);
            Assert.InRange(size, 2f, 40f);
            Assert.InRange(count, 1, 50);
        }
        Assert.True(plan.Skip(1).Select(p => p.Tweaks["Size"]).Distinct().Count() > 10);
    }

    [Fact]
    public void The_same_plan_gives_the_same_sheet()
    {
        var a = VariationRenderer.Plan(NewField, 4, VariationMode.SeedsAndTweaks, 9);
        var b = VariationRenderer.Plan(NewField, 4, VariationMode.SeedsAndTweaks, 9);
        Assert.Equal(a.Select(p => p.Seed), b.Select(p => p.Seed));
        Assert.Equal(a.Select(p => string.Join(",", p.Tweaks.Values)), b.Select(p => string.Join(",", p.Tweaks.Values)));
    }

    [Fact]
    public void A_variation_renders_the_same_every_time_and_differs_from_another_seed()
    {
        var empty = new Dictionary<string, string>();
        var one = VariationRenderer.Render(NewField, 0, 1234, empty, frames: 10, thumbnailSize: 80);
        var again = VariationRenderer.Render(NewField, 0, 1234, empty, frames: 10, thumbnailSize: 80);
        var other = VariationRenderer.Render(NewField, 1, 4321, empty, frames: 10, thumbnailSize: 80);

        Assert.Null(one.Error);
        Assert.Equal(80, one.Thumbnail!.Width);                    // 160x100 shrunk to fit 80
        Assert.Equal(50, one.Thumbnail.Height);
        Assert.Equal(Bytes(one.Thumbnail), Bytes(again.Thumbnail!));
        Assert.NotEqual(Bytes(one.Thumbnail), Bytes(other.Thumbnail!));
    }

    public class SetupReads : Sketch
    {
        [Tweak(1, 100)] public int Count = 5;
        public static int Seen;
        public override void Setup() => Seen = Count;
        public override void Draw() { }
    }

    [Fact]
    public void Tweak_values_are_in_place_before_Setup()
    {
        VariationRenderer.Render(() => new SetupReads(), 0, 1, new Dictionary<string, string> { ["Count"] = "42" }, frames: 1);
        Assert.Equal(42, SetupReads.Seen);
    }

    public class Broken : Sketch
    {
        public override void Setup() => throw new InvalidOperationException("nope");
        public override void Draw() { }
    }

    [Fact]
    public void A_failing_sketch_becomes_an_error_cell()
    {
        var v = VariationRenderer.Render(() => new Broken(), 3, 1, new Dictionary<string, string>());
        Assert.Null(v.Thumbnail);
        Assert.Contains("nope", v.Error);
        Assert.Equal(3, v.Index);
    }

    public class Greedy : Sketch
    {
        public override void Setup() => ListenTo(AudioSource.Microphone);
        public override void Draw() { _ = MidiCC(1); }
    }

    [Fact]
    public void Variations_never_open_the_microphone_or_midi()
    {
        int listeners = Microphone.Listeners;
        var v = VariationRenderer.Render(() => new Greedy(), 0, 1, new Dictionary<string, string>(), frames: 3);
        Assert.Null(v.Error);
        Assert.Equal(listeners, Microphone.Listeners);
    }
}
