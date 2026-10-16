using DanaProcessing.Tests.Support;
using SkiaSharp;
using Xunit;

namespace DanaProcessing.Tests.Core;

/// <summary>OnStop() and LoadImage(byte[]) -- what device-using sketches (camera, serial, MIDI) rely on.</summary>
public class LifecycleTests
{
    public class Stoppable : Sketch
    {
        public int Stops;
        public override void Draw() { }
        public override void OnStop() => Stops++;
    }

    public class Drawn : Sketch
    {
        public override void Setup() => Size(40, 30);
        public override void Draw() => Background(255, 0, 0);
    }

    [Fact]
    public void OnStop_runs_once_no_matter_how_often_the_host_asks()
    {
        var sketch = new Stoppable();
        sketch.RaiseStopped();
        sketch.RaiseStopped();
        Assert.Equal(1, sketch.Stops);
    }

    [Fact]
    public void LoadImage_decodes_encoded_bytes()
    {
        byte[] png;
        using (var surface = Offscreen.Run(new Drawn()))
        using (var image = surface.Snapshot())
        using (var data = image.Encode(SKEncodedImageFormat.Png, 100))
            png = data.ToArray();

        var sketch = new Drawn();
        using var loaded = sketch.LoadImage(png);
        Assert.Equal(40, loaded.Width);
        Assert.Equal(30, loaded.Height);
    }

    [Fact]
    public void LoadImage_rejects_bytes_that_are_not_an_image()
    {
        var sketch = new Drawn();
        Assert.Throws<InvalidOperationException>(() => sketch.LoadImage(new byte[] { 1, 2, 3, 4, 5 }));
    }
}
