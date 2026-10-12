using DanaProcessing.Ide.Compilation;
using Xunit;

namespace DanaProcessing.Tests.Ide;

public class SketchCompilerTests
{
    [Fact]
    public void Compiles_a_minimal_sketch_with_implicit_usings()
    {
        var result = SketchCompiler.Compile("""
            public class Hello : Sketch
            {
                readonly List<PVector> points = new();   // System.Collections.Generic is implicit
                public override void Setup() => Size(200, 200);
                public override void Draw() => Circle(Width / 2, Height / 2, 50);
            }
            """);
        Assert.True(result.Success, string.Join("\n", result.Errors));
        Assert.Equal("Hello", result.Sketch!.GetType().Name);
    }

    [Fact]
    public void Reports_errors_with_their_line()
    {
        var result = SketchCompiler.Compile("""
            public class Broken : Sketch
            {
                public override void Draw()
                {
                    int x = "not a number";
                }
            }
            """);
        Assert.False(result.Success);
        Assert.True(result.Errors.Any(e => e.Contains("Sketch.cs(5,")), string.Join("\n", result.Errors));
    }

    [Fact]
    public void Sketches_are_compiled_as_CSharp_14()
    {
        // The `field` keyword in an accessor is C# 14.
        var result = SketchCompiler.Compile("""
            public class Modern : Sketch
            {
                public int Count { get; set => field = Math.Max(0, value); }
                public override void Draw() { }
            }
            """);
        Assert.True(result.Success, string.Join("\n", result.Errors));
    }

    [Fact]
    public void Reel_markers_and_the_reels_module_compile()
    {
        var result = SketchCompiler.Compile("""
            using DanaProcessing.Reels;
            // @reel title: Test
            // @reel scene "Draw"
            public class R : Sketch
            {
                public override void Draw() { if (Reel.IsActive) Background(0); }
            }
            """);
        Assert.True(result.Success, string.Join("\n", result.Errors));
    }

    [Fact]
    public void No_sketch_class_is_an_error()
    {
        var result = SketchCompiler.Compile("public class NotASketch { }");
        Assert.False(result.Success);
        Assert.Single(result.Errors);
    }

    [Fact]
    public void Every_bundled_sample_compiles()
    {
        var failures = new List<string>();
        foreach (var sample in DanaProcessing.Ide.Editor.SketchSamples.All)
        {
            // Samples that pull NuGet packages need the network: compile-check the rest.
            if (sample.Source.Contains("// nuget:"))
                continue;
            var r = SketchCompiler.Compile(sample.Source);
            if (!r.Success)
                failures.Add(sample.Name + ": " + string.Join(" | ", r.Errors.Take(2)));
        }
        Assert.True(failures.Count == 0, string.Join("\n", failures));
    }
}

public class SketchCrashInfoTests
{
    [Fact]
    public void Runtime_errors_point_at_the_sketch_line()
    {
        var result = SketchCompiler.Compile("""
            public class Crashy : Sketch
            {
                public override void Draw()
                {
                    int[] values = new int[3];
                    values[5] = 1;
                }
            }
            """);
        Assert.True(result.Success, string.Join("\n", result.Errors));
        Exception? caught = null;
        try { result.Sketch!.Draw(); } catch (Exception ex) { caught = ex; }
        Assert.NotNull(caught);
        var info = SketchCrashInfo.From(caught!, "Draw");
        Assert.Equal(6, info.Line);
        Assert.Equal("IndexOutOfRangeException", info.ExceptionName);
    }

    [Fact]
    public void Failures_outside_the_sketch_have_no_line()
    {
        var info = SketchCrashInfo.From(new InvalidOperationException("library"), "Setup");
        Assert.Equal(0, info.Line);
        Assert.Equal("Setup", info.Context);
    }
}
