using DanaProcessing.Ide.Compilation;
using DanaProcessing.Tests.Support;
using Xunit;

namespace DanaProcessing.Tests.Ide;

/// <summary>
/// The attached-sketch samples, compiled like the IDE does and run against a
/// fake terminal: dragged, shaken, typed into, clicked, hidden, resized.
/// </summary>
public class AttachedSampleTests
{
    public static IEnumerable<object[]> AttachedSamples() =>
        DanaProcessing.Ide.Editor.SketchSamples.All
            .Where(s => s.Source.Contains(": AttachedSketch") && !s.Source.Contains("// nuget:"))   // NuGet samples: see the audio test
            .Select(s => new object[] { s.Name });

    private static AttachedSketch Compile(string name)
    {
        var sample = DanaProcessing.Ide.Editor.SketchSamples.All.Single(s => s.Name == name);
        var result = SketchCompiler.Compile(sample.Source);
        Assert.True(result.Success, string.Join("\n", result.Errors));
        return Assert.IsAssignableFrom<AttachedSketch>(result.Sketch);
    }

    [Fact]
    public void There_are_attached_samples()
    {
        Assert.True(AttachedSamples().Count() >= 4);
    }

    [Theory]
    [MemberData(nameof(AttachedSamples))]
    public void Survives_a_whole_session_on_a_fake_terminal(string name)
    {
        var d = new AttachedDriver(Compile(name));
        Assert.Equal("CASCADIA_HOSTING_WINDOW_CLASS", d.Sketch.Target.ClassName);

        d.Frames(10);
        for (int i = 0; i < 30; i++) { d.X += 15; d.Frame(); }   // drag right
        d.Frames(20);                                           // sudden stop
        for (int i = 0; i < 20; i++) { d.Y += i % 2 == 0 ? 40 : -40; d.Frame(); } // shake
        d.CursorX = 300; d.CursorY = 200;
        d.Type("ls -la\n");
        d.Type("oops\b\b");
        d.Click(200, 150);
        d.Focused = false; d.Frames(10);
        d.Showing = false; d.Frames(5);
        d.Showing = true; d.Focused = true; d.Frames(10);
        d.Resize(1100, 300);
        d.Frames(30);
        d.Resize(400, 600);
        d.Frames(30);

        Assert.True(d.Sketch.FrameCount > 150);
        Assert.True(d.PaintedPixels() > 0, "drew nothing");
    }

    [Fact]
    public void Water_sloshes_against_the_wall_when_the_terminal_stops()
    {
        var d = new AttachedDriver(Compile(DanaProcessing.Ide.Editor.SketchSamples.All
            .Single(s => s.Source.Contains("class TerminalWater")).Name), 800, 400);
        d.Frames(30);
        for (int i = 0; i < 40; i++) { d.X += 20; d.Frame(); }   // 1200 px/s to the right

        // Starting to move left the water behind: piled up on the left wall.
        int left = Column(d, 40), right = Column(d, 760);
        Assert.True(left > right + 50, $"while dragging: left {left} px, right {right} px");

        for (int i = 0; i < 40; i++) d.Frame();                 // stop dead, wait 2/3 s

        // The stop throws it the other way: now it's on the right wall.
        left = Column(d, 40);
        right = Column(d, 760);
        Assert.True(right > left + 20, $"after the stop: left {left} px, right {right} px");
    }

    [Fact]
    public void Keyboard_bubbles_appear_for_typed_letters_and_pop_on_enter()
    {
        var d = new AttachedDriver(Compile(DanaProcessing.Ide.Editor.SketchSamples.All
            .Single(s => s.Source.Contains("class TerminalKeys")).Name));
        d.Frames(5);
        int empty = d.PaintedPixels();
        d.Type("hola");
        d.Frames(5);
        Assert.True(d.PaintedPixels() > empty + 50);
        Assert.True(d.Sketch.ListensToTargetKeys);
        Assert.True(d.Sketch.ListensToTargetMouse);
    }

    private static AttachedSketch CompileByClass(string className) =>
        Compile(DanaProcessing.Ide.Editor.SketchSamples.All.Single(s => s.Source.Contains("class " + className + " ")).Name);

    [Fact]
    public void Flow_field_runs_one_per_window_and_listens_to_the_shell()
    {
        var d = new AttachedDriver(CompileByClass("TerminalFlowField"));
        Assert.Equal(AttachMode.EachWindow, d.Sketch.Mode);
        Assert.True(d.Sketch.ListensToShell);
    }

    [Fact]
    public void Flow_field_turns_red_when_a_command_fails_and_calms_down_after_one_works()
    {
        var d = new AttachedDriver(CompileByClass("TerminalFlowField"), 600, 400);
        d.Frames(40);
        double calm = RedShare(d);
        Assert.True(calm < 0.2, $"red share before any error: {calm:P0}");

        d.CommandFailed(3);
        d.Frames(20);
        double storm = RedShare(d);
        Assert.True(storm > 0.5, $"red share during the storm: {storm:P0}");
        Assert.Equal(3, d.Sketch.LastExitCode);

        d.CommandSucceeded();
        d.Frames(200);  // the storm fades out in 2.5 s
        Assert.True(RedShare(d) < 0.2, $"red share after it calmed down: {RedShare(d):P0}");
    }

    [Fact]
    public void Flow_field_particles_lag_behind_when_the_terminal_is_dragged_and_swing_back_when_it_stops()
    {
        var still = new AttachedDriver(CompileByClass("TerminalFlowField"), 600, 400, seed: 7);
        var dragged = new AttachedDriver(CompileByClass("TerminalFlowField"), 600, 400, seed: 7);
        still.Frames(30);
        dragged.Frames(30);
        for (int i = 0; i < 6; i++)
        {
            dragged.X += 40;   // yanked to the right
            dragged.Frame();
            still.Frame();
        }
        // Inertia: in the window's frame the particles head left, the other way.
        Assert.True(MeanVx(dragged) < MeanVx(still) - 150, $"while dragging: {MeanVx(dragged):0} vs {MeanVx(still):0} px/s");

        for (int i = 0; i < 14; i++)
        {
            dragged.X += 40;
            dragged.Frame();
            still.Frame();
        }
        for (int i = 0; i < 6; i++)
        {
            dragged.Frame();   // stopped dead
            still.Frame();
        }
        Assert.True(MeanVx(dragged) > MeanVx(still) + 50, $"after the stop: {MeanVx(dragged):0} vs {MeanVx(still):0} px/s");
    }

    /// <summary>Mean horizontal speed of the sample's particles (read from its private list: a white-box check of the physics).</summary>
    private static double MeanVx(AttachedDriver d)
    {
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var particles = (System.Collections.IEnumerable)d.Sketch.GetType().GetField("_particles", flags)!.GetValue(d.Sketch)!;
        double sum = 0;
        int n = 0;
        foreach (var p in particles)
        {
            sum += (float)p.GetType().GetField("VX")!.GetValue(p)!;
            n++;
        }
        return n == 0 ? 0 : sum / n;
    }

    /// <summary>Of the painted pixels, how many are clearly red (the storm palette).</summary>
    private static double RedShare(AttachedDriver d)
    {
        using var image = d.Surface.Snapshot();
        using var bitmap = SkiaSharp.SKBitmap.FromImage(image);
        int painted = 0, red = 0;
        for (int y = 0; y < bitmap.Height; y += 2)
            for (int x = 0; x < bitmap.Width; x += 2)
            {
                var c = bitmap.GetPixel(x, y);
                if (c.Alpha < 40) continue;
                painted++;
                if (c.Red > c.Blue + 40 && c.Red > c.Green) red++;
            }
        return painted == 0 ? 0 : (double)red / painted;
    }

    // --- the samples inspired by OpenProcessing (cloth, blobs, wobbly functions) ---

    private static object? Field(object o, string name) =>
        o.GetType().GetField(name, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic)!.GetValue(o);

    private static List<object> Blobs(AttachedDriver d) =>
        ((System.Collections.IEnumerable)Field(d.Sketch, "_blobs")!).Cast<object>().ToList();

    [Fact]
    public void Blobs_float_instead_of_lying_on_the_floor()
    {
        var d = new AttachedDriver(CompileByClass("TerminalBlobs"), 1000, 560, seed: 3);
        d.Frames(240);
        float meanY = Blobs(d).Average(b => (float)Field(b, "Y")!);
        Assert.True(meanY < 560 * 0.75f, $"blobs sit at y={meanY} of 560");
    }

    [Fact]
    public void Blobs_are_thrown_the_other_way_when_the_terminal_is_dragged()
    {
        var d = new AttachedDriver(CompileByClass("TerminalBlobs"), 1000, 560, seed: 3);
        d.Frames(60);
        for (int i = 0; i < 6; i++) { d.X += 30; d.Frame(); }
        float vx = Blobs(d).Average(b => (float)Field(b, "VX")!);
        Assert.True(vx < -150, $"mean VX {vx}");
    }

    [Fact]
    public void Blobs_burst_apart_on_a_failed_command_and_merge_again()
    {
        var d = new AttachedDriver(CompileByClass("TerminalBlobs"), 1000, 560, seed: 3);
        d.Frames(180);
        int before = Blobs(d).Count;

        d.CommandFailed(1);
        d.Frame();
        int burst = Blobs(d).Count;
        Assert.True(burst >= before * 2, $"{before} -> {burst}");
        d.Frames(60);   // the color eases to red in about a second
        Assert.True(RedShare(d) > 0.3, $"the droplets turn red ({RedShare(d):0.00})");

        d.Frames(60 * 7);
        int after = Blobs(d).Count;
        Assert.True(after <= before + 2 && after < burst, $"{before} -> {burst} -> {after}: they should merge again");
    }

    [Fact]
    public void Cloth_covers_the_whole_terminal()
    {
        var d = new AttachedDriver(CompileByClass("TerminalCloth"), 900, 500, seed: 1);
        d.Frames(60);
        using var image = d.Surface.Snapshot();
        using var bitmap = SkiaSharp.SKBitmap.FromImage(image);
        int covered = 0, total = 0;
        for (int y = 5; y < 495; y += 7)
            for (int x = 5; x < 895; x += 7, total++)
                if (bitmap.GetPixel(x, y).Alpha > 0) covered++;
        Assert.True(covered > total * 0.95, $"{covered}/{total} covered");
        Assert.True(bitmap.GetPixel(890, 490).Alpha > 0 && bitmap.GetPixel(8, 490).Alpha > 0, "bottom corners");
    }

    [Fact]
    public void Cloth_tears_on_a_failed_command()
    {
        var d = new AttachedDriver(CompileByClass("TerminalCloth"), 900, 500, seed: 1);
        d.Frames(30);
        int painted = d.PaintedPixels();
        d.CommandFailed(1);
        d.Frames(30);
        Assert.True(d.PaintedPixels() < painted * 0.97, "a hole opens");
    }

    [Fact]
    public void Wobbly_strings_are_plucked_by_keys()
    {
        var d = new AttachedDriver(CompileByClass("TerminalWobble"), 900, 500, seed: 1);
        d.Frames(30);
        float Energy() { var w = (float[,])Field(d.Sketch, "_wave")!; float e = 0; foreach (var v in w) e += Math.Abs(v); return e; }
        float calm = Energy();
        d.Type("hola\n");
        d.Frames(5);
        Assert.True(Energy() > calm + 50, $"{calm} -> {Energy()}");
    }

    [Fact]
    public void Css_blob_morphs_between_keyframes_and_trails_a_drag()
    {
        var d = new AttachedDriver(CompileByClass("TerminalMorphBlob"), 900, 500, seed: 1);
        d.Frames(30);
        Assert.True(d.PaintedPixels() > (900 / 2) * (500 / 2) / 20, "the blob is drawn");   // PaintedPixels samples every other pixel
        float homeX = (float)Field(d.Sketch, "_x")!;
        float morph = (float)Field(d.Sketch, "_morph")!;
        for (int i = 0; i < 8; i++) { d.X += 30; d.Frame(); }
        Assert.True((float)Field(d.Sketch, "_x")! < homeX - 5, "it lags behind a drag to the right");
        Assert.True((float)Field(d.Sketch, "_morph")! > morph, "it keeps morphing");
        d.CommandFailed(1);
        d.Frames(5);
        Assert.True(RedShare(d) > 0.3, $"a failed command turns it red ({RedShare(d):0.00})");
    }

    [Fact]
    public void Audio_terminal_sample_reacts_to_its_spectrum_and_floats_free_in_silence()
    {
        var sample = DanaProcessing.Ide.Editor.SketchSamples.All.Single(s => s.Source.Contains("class TerminalAudio "));
        Assert.Contains("// nuget: NAudio", sample.Source);
        Assert.Contains("OnStop() => StopCapture()", sample.Source);

        // Needs NAudio from nuget.org, like Run does; offline there's nothing more to check.
        var directives = DanaProcessing.Ide.Compilation.PackageManagement.PackageDirectiveParser.Parse(sample.Source);
        DanaProcessing.Ide.Compilation.PackageManagement.NuGetResolutionResult resolution;
        try
        {
            var task = DanaProcessing.Ide.Compilation.PackageManagement.NuGetPackageResolver.ResolveAsync(directives);
            if (!task.Wait(TimeSpan.FromSeconds(60))) return;
            resolution = task.Result;
        }
        catch { return; }
        if (!resolution.Success) return;
        SketchCompiler.SetNuGetAssemblies(resolution.Packages);
        var result = SketchCompiler.Compile(sample.Source, resolution.AllAssemblyPaths
            .Select(p => (Microsoft.CodeAnalysis.MetadataReference)Microsoft.CodeAnalysis.MetadataReference.CreateFromFile(p)).ToList());
        Assert.True(result.Success, string.Join("\n", result.Errors));

        var d = new AttachedDriver(Assert.IsAssignableFrom<AttachedSketch>(result.Sketch), 900, 500, seed: 1);
        d.Frame();
        d.Sketch.GetType().GetMethod("StopCapture", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.Invoke(d.Sketch, null);
        var bars = (float[])Field(d.Sketch, "_bars")!;
        for (int f = 0; f < 60; f++) { for (int i = 0; i < bars.Length; i++) bars[i] = 0.6f; d.Frame(); }
        var level = (float[])Field(d.Sketch, "_level")!;
        Assert.True(level.Average() > 0.5f, "loud bands raise the blobs");
        Assert.True((float)Field(d.Sketch, "_free")! < 0.2f, "with music they stay on their stalks");

        Array.Clear(bars);
        d.Frames(180);
        Assert.True((float)Field(d.Sketch, "_free")! > 0.8f, "in silence they come loose");
        d.Sketch.RaiseStopped();   // OnStop(): capture stopped, nothing left running
    }

    /// <summary>Height of painted pixels (water) in a column, counted from the bottom.</summary>
    private static int Column(AttachedDriver d, int x)
    {
        using var image = d.Surface.Snapshot();
        using var bitmap = SkiaSharp.SKBitmap.FromImage(image);
        int n = 0;
        for (int y = bitmap.Height - 1; y >= 0 && bitmap.GetPixel(x, y).Alpha > 0; y--)
            n++;
        return n;
    }
}
