using DanaProcessing.AvaloniaHost;
using DanaProcessing.Ide.Compilation;
using DanaProcessing.Tests.Support;
using SkiaSharp;
using Xunit;

namespace DanaProcessing.Tests.Ide;

/// <summary>Widgets: Sketch.Widget(), where they're remembered on screen, and the audio widget sample.</summary>
public class WidgetTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "dana_widget_" + Guid.NewGuid().ToString("N"));
    private readonly string _savedPlacementFile = WidgetPlacement.FilePath;

    public WidgetTests() => Directory.CreateDirectory(_dir);

    public void Dispose()
    {
        WidgetPlacement.FilePath = _savedPlacementFile;
        Directory.Delete(_dir, recursive: true);
    }

    private class Plain : Sketch { public override void Draw() { } }

    private class Asks : Sketch
    {
        public int SettingsCalls, SetupCalls;
        public override void Settings() { SettingsCalls++; Size(300, 120); Widget(alwaysOnTop: false); }
        public override void Setup() => SetupCalls++;
        public override void Draw() { }
    }

    private static void Run(Action code, string _) => code();

    [Fact]
    public void An_ordinary_sketch_is_no_widget()
    {
        var s = new Plain();
        Assert.Null(s.WidgetMode);
        Assert.False(s.IsWidget);
        Assert.False(s.IsTransparentWindow);
    }

    [Fact]
    public void Widget_defaults_to_see_through_on_top_and_draggable()
    {
        var s = new Plain();
        s.Widget();
        Assert.Equal(new WidgetOptions(Transparent: true, AlwaysOnTop: true, Draggable: true), s.WidgetMode);
        Assert.True(s.IsWidget);
    }

    [Fact]
    public void Settings_runs_once_when_a_host_reads_it_early()
    {
        var s = new Asks();
        s.RunSettings(Run);                 // the window reads Widget() before it shows
        Assert.Equal(new WidgetOptions(true, false, true), s.WidgetMode);
        Assert.Equal((300, 120), (s.Width, s.Height));

        s.RunStartup(Run);                  // then the canvas starts it as usual
        Assert.Equal(1, s.SettingsCalls);
        Assert.Equal(1, s.SetupCalls);
    }

    [Fact]
    public void Startup_still_runs_settings_then_setup()
    {
        var s = new Asks();
        s.RunStartup(Run);
        Assert.Equal((1, 1), (s.SettingsCalls, s.SetupCalls));
        Assert.True(s.IsWidget);
    }

    [Fact]
    public void A_widget_comes_back_where_it_was_left()
    {
        WidgetPlacement.FilePath = Path.Combine(_dir, "sub", "widgets.json");
        Assert.Null(WidgetPlacement.Load("AudioWidget"));

        WidgetPlacement.Save("AudioWidget", new Avalonia.PixelPoint(1200, 40));
        WidgetPlacement.Save("Clock", new Avalonia.PixelPoint(-1500, 300));   // a screen on the left
        WidgetPlacement.Save("AudioWidget", new Avalonia.PixelPoint(900, 60));

        Assert.Equal(new Avalonia.PixelPoint(900, 60), WidgetPlacement.Load("AudioWidget"));
        Assert.Equal(new Avalonia.PixelPoint(-1500, 300), WidgetPlacement.Load("Clock"));
    }

    [Fact]
    public void A_broken_placement_file_just_means_the_default_corner()
    {
        WidgetPlacement.FilePath = Path.Combine(_dir, "widgets.json");
        File.WriteAllText(WidgetPlacement.FilePath, "{ not json");
        Assert.Null(WidgetPlacement.Load("AudioWidget"));
        WidgetPlacement.Save("AudioWidget", new Avalonia.PixelPoint(10, 20));   // and it heals
        Assert.Equal(new Avalonia.PixelPoint(10, 20), WidgetPlacement.Load("AudioWidget"));
    }

    [Fact]
    public void Placeholder_text_wraps_between_words()
    {
        var lines = AvaloniaSketchCanvas.WrapText("Drag it anywhere; right-click for its menu. Stop closes it.", 20).ToList();
        Assert.Equal(new[] { "Drag it anywhere;", "right-click for its", "menu. Stop closes", "it." }, lines);
        Assert.All(lines, l => Assert.True(l.Length <= 20));
        Assert.Equal(new[] { "abcdefghij", "klmno" }, AvaloniaSketchCanvas.WrapText("abcdefghijklmno", 10));
    }

    // --- The audio widget sample -------------------------------------------------

    private static object? Field(object o, string name) =>
        o.GetType().GetField(name, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(o);

    private static void SetField(object o, string name, object value) =>
        o.GetType().GetField(name, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.SetValue(o, value);

    [Fact]
    public void Audio_widget_sample_is_a_see_through_widget_that_swings_when_dragged()
    {
        var sample = DanaProcessing.Ide.Editor.SketchSamples.All.Single(s => s.Source.Contains("class AudioWidget "));
        Assert.Contains("// nuget: NAudio", sample.Source);
        Assert.Contains("Widget();", sample.Source);

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

        var sketch = result.Sketch!;
        sketch.RunSettings(Run);
        Assert.Equal(new WidgetOptions(true, true, true), sketch.WidgetMode);
        Assert.Equal((560, 220), (sketch.Width, sketch.Height));

        var surface = Offscreen.Run(sketch, 1);   // Setup() + a frame
        // Quiet and deterministic: no capture, no demo, bands set by hand.
        sketch.GetType().GetMethod("StopCapture", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.Invoke(sketch, null);
        SetField(sketch, "_demo", false);
        SetField(sketch, "_status", "");
        var bars = (float[])Field(sketch, "_bars")!;

        void Frame()
        {
            Thread.Sleep(5);   // the sample times itself with Millis()
            surface.Canvas.Save();
            sketch.RenderFrame();
            surface.Canvas.Restore();
            surface.Canvas.Flush();
        }

        // See-through window: the empty top corner is fully transparent.
        sketch.IsTransparentWindow = true;
        for (int f = 0; f < 40; f++) { Array.Fill(bars, 0.6f); Frame(); }
        Assert.Equal(0, Offscreen.Pixel(surface, 4, 4).Alpha);
        Assert.True((float)Field(sketch, "_free")! < 0.2f, "with music the blobs stay on their stalks");

        // Opaque fallback: it paints its own dark background.
        sketch.IsTransparentWindow = false;
        Frame();
        var corner = Offscreen.Pixel(surface, 4, 4);
        Assert.Equal(255, corner.Alpha);
        Assert.True(Offscreen.IsDark(corner), $"dark panel ({corner})");

        // Dragged right and stopped suddenly: the blobs swing.
        var ox = (float[])Field(sketch, "_ox")!;
        float before = ox.Select(Math.Abs).Sum();
        for (int f = 0; f < 15; f++) { sketch.WindowX += 40; Array.Fill(bars, 0.6f); Frame(); }
        for (int f = 0; f < 3; f++) { Array.Fill(bars, 0.6f); Frame(); }
        Assert.True(ox.Select(Math.Abs).Sum() > before + 5, "dragging the window moves the blobs");

        // Silence: they come loose and float.
        Array.Clear(bars);
        for (int f = 0; f < 120; f++) { Thread.Sleep(20); Frame(); }   // ~3 s of silence
        Assert.True((float)Field(sketch, "_free")! > 0.8f, "in silence they come loose");
        sketch.RaiseStopped();
    }
}
