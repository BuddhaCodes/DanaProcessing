using System.Diagnostics;
using DanaProcessing.Ide.Compilation;
using DanaProcessing.Tests.Support;
using Xunit;
using Xunit.Abstractions;

namespace DanaProcessing.Tests.Core;

/// <summary>
/// Baselines, not micro-benchmarks: generous limits that only fail on a real
/// regression (CI machines are slow and noisy), and the measured numbers
/// printed to the test log so they can be tracked over time.
/// Run just these with: dotnet test --filter "FullyQualifiedName~PerformanceTests"
/// </summary>
public class PerformanceTests
{
    private readonly ITestOutputHelper _log;
    public PerformanceTests(ITestOutputHelper log) => _log = log;

    private const string Trivial = """
        public class T : Sketch
        {
            float a;
            public override void Draw() { a += 0.1f; }
        }
        """;

    [Fact]
    public void Compile_time_for_Run()
    {
        var sw = Stopwatch.StartNew();
        Assert.True(SketchCompiler.Compile(Trivial).Success);
        var cold = sw.Elapsed.TotalMilliseconds;

        var warm = new List<double>();
        for (int i = 0; i < 5; i++)
        {
            sw.Restart();
            Assert.True(SketchCompiler.Compile(Trivial).Success);
            warm.Add(sw.Elapsed.TotalMilliseconds);
        }
        _log.WriteLine($"compile: first {cold:0} ms, then {warm.Average():0} ms on average (min {warm.Min():0})");
        Assert.True(warm.Average() < 5000, $"warm compile took {warm.Average():0} ms");
    }

    [Fact]
    public void Repeated_runs_do_not_pile_up_in_memory()
    {
        for (int i = 0; i < 25; i++)
        {
            var r = SketchCompiler.Compile(Trivial);
            Assert.True(r.Success);
            r.Sketch!.Draw();
        }
        for (int i = 0; i < 5 && SketchCompiler.LoadedSketchAssemblies > 6; i++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
        }
        _log.WriteLine($"after 25 runs: {SketchCompiler.LoadedSketchAssemblies} sketch assemblies still loaded");
        Assert.InRange(SketchCompiler.LoadedSketchAssemblies, 0, 6);
    }

    private sealed class Circles : Sketch
    {
        private readonly int _count;
        public Circles(int count) => _count = count;
        public override void Setup() { Size(800, 600); NoStroke(); }
        public override void Draw()
        {
            Background(20);
            for (int i = 0; i < _count; i++)
            {
                FillHSB(i % 360, 70, 100, 180);
                Circle((i * 37) % 800, (i * 53) % 600, 8);
            }
        }
    }

    [Theory]
    [InlineData(1000)]
    [InlineData(5000)]
    public void Two_d_drawing_frame_time(int circles)
    {
        var sketch = new Circles(circles);
        using var surface = Offscreen.Run(sketch, 5); // warm-up
        var sw = Stopwatch.StartNew();
        const int frames = 60;
        for (int i = 0; i < frames; i++)
        {
            surface.Canvas.Save();
            sketch.RenderFrame();
            surface.Canvas.Restore();
        }
        surface.Canvas.Flush();
        double ms = sw.Elapsed.TotalMilliseconds / frames;
        _log.WriteLine($"{circles} circles: {ms:0.00} ms/frame ({1000 / ms:0} fps possible, 60 fps needs 16.7 ms)");
        Assert.True(ms < 200, $"{ms:0.0} ms per frame");
    }
}
