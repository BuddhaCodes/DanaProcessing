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
        // The count is process-wide: sketches compiled by tests that ran before
        // this one (and may still be referenced) count too. Measure only what
        // these 25 runs leave behind.
        int before = LoadedAfterGc(int.MaxValue);

        for (int i = 0; i < 25; i++)
            CompileAndDraw();

        int after = LoadedAfterGc(before + 6);
        int added = after - before;
        string alive = string.Join(", ", SketchCompiler.LoadedSketchTypeNames()
            .GroupBy(n => n).Select(g => $"{g.Key} x{g.Count()}"));
        _log.WriteLine($"before: {before}, after 25 runs: {after} sketch assemblies still loaded ({alive})");
        Assert.True(added <= 6, $"25 runs left {added} sketch assemblies loaded (before {before}, after {after}): {alive}");
    }

    // Its own method so no local in the test keeps the last sketch alive.
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static void CompileAndDraw()
    {
        var r = SketchCompiler.Compile(Trivial);
        Assert.True(r.Success);
        r.Sketch!.Draw();
    }

    private static int LoadedAfterGc(int target)
    {
        for (int i = 0; i < 5 && SketchCompiler.LoadedSketchAssemblies > target; i++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
        }
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        return SketchCompiler.LoadedSketchAssemblies;
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
