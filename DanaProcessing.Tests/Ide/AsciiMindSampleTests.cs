using DanaProcessing.Ide.Compilation;
using DanaProcessing.Ide.Editor;
using DanaProcessing.Reels;
using Xunit;

namespace DanaProcessing.Tests.Ide;

/// <summary>
/// "An AI learns to write in ASCII": a hand-written neural network in the
/// sketch learns the typed word live. These run the sample for real (headless,
/// through the same stage reels use) and check that it actually learns: the
/// error falls, the ink ends up inside the letters, and a new word is learned
/// on top of the old one.
/// </summary>
public class AsciiMindSampleTests
{
    private static SketchSample Sample => SketchSamples.All.Single(s => s.Source.Contains("class AsciiMind"));

    private sealed class Run : IDisposable
    {
        public readonly Sketch Sketch;
        private readonly SketchStage _stage;
        private double _t;

        public Run()
        {
            var result = SketchCompiler.Compile(Sample.Source);
            Assert.True(result.Success, string.Join("\n", result.Errors));
            var type = result.Sketch!.GetType();
            Sketch? made = null;
            _stage = new SketchStage(() => made = (Sketch)Activator.CreateInstance(type)!, ReelScript.Parse("// @reel mouse: off"), 60)
            {
                MaxFramesPerAdvance = 100000,
            };
            _stage.AdvanceTo(0);
            Sketch = made!;
        }

        public void Seconds(double s)
        {
            _t += s;
            _stage.AdvanceTo(_t);
            Assert.Null(_stage.Error);
        }

        public T Get<T>(string property) => (T)Sketch.GetType().GetProperty(property)!.GetValue(Sketch)!;
        public float Ink(int col, int row) => (float)Sketch.GetType().GetMethod("InkAt")!.Invoke(Sketch, new object[] { col, row })!;
        public void Learn(string word) => Sketch.GetType().GetMethod("Learn")!.Invoke(Sketch, new object[] { word });

        public void Dispose() => _stage.Dispose();
    }

    [Fact]
    public void It_is_listed_as_ml_and_text_and_needs_no_package()
    {
        Assert.Contains(SampleTag.MachineLearning, Sample.Tags);
        Assert.Contains(SampleTag.Text, Sample.Tags);
        Assert.DoesNotContain("// nuget:", Sample.Source);   // the network is plain C#: works offline, first run
    }

    [Fact]
    public void The_network_learns_the_word_and_its_error_falls()
    {
        using var run = new Run();
        Assert.Equal("HOLA", run.Get<string>("Word"));

        run.Seconds(0.2);
        float early = run.Get<float>("Loss");
        run.Seconds(3);
        float late = run.Get<float>("Loss");
        Assert.True(run.Get<int>("Steps") > 500, $"steps {run.Get<int>("Steps")}");
        Assert.True(late < early / 4, $"error should drop: {early:0.0000} -> {late:0.0000}");
        Assert.True(late < 0.01f, $"error after 3 s {late:0.0000}");

        // The ink is where the letters are: the H's left stem, not the corners of the screen.
        // (96 x 30 characters; HOLA spans ~86 % of the width, centered.)
        float stem = Enumerable.Range(10, 10).Average(r => Math.Max(run.Ink(8, r), Math.Max(run.Ink(9, r), run.Ink(10, r))));
        float corner = new[] { run.Ink(1, 1), run.Ink(94, 1), run.Ink(1, 28), run.Ink(94, 28) }.Average();
        Assert.True(stem > 0.5f, $"H stem ink {stem:0.00}");
        Assert.True(corner < 0.1f, $"corner ink {corner:0.00}");
    }

    [Fact]
    public void A_new_word_is_learned_on_top_of_the_old_one()
    {
        using var run = new Run();
        run.Seconds(3);
        float learned = run.Get<float>("Loss");
        int steps = run.Get<int>("Steps");

        run.Learn("dana");
        Assert.Equal("DANA", run.Get<string>("Word"));
        run.Seconds(0.3);
        float surprised = run.Get<float>("Loss");
        Assert.True(surprised > learned * 2, $"the old word is wrong for the new one: {learned:0.0000} -> {surprised:0.0000}");

        run.Seconds(4);
        Assert.True(run.Get<int>("Steps") > steps, "it keeps the same network (no restart)");
        Assert.True(run.Get<float>("Loss") < surprised / 3, $"and learns the new word: {surprised:0.0000} -> {run.Get<float>("Loss"):0.0000}");
    }
}
