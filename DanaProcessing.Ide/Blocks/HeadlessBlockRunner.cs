using System;
using System.Collections.Generic;
using System.Linq;
using DanaProcessing.Ide.Compilation;
using SkiaSharp;

namespace DanaProcessing.Ide.Blocks
{
    /// <summary>What a block program did when run without a window.</summary>
    public sealed record HeadlessRunResult(bool Compiled, IReadOnlyList<string> Errors, IReadOnlyList<TurtleLine> TurtleLines);

    /// <summary>
    /// Runs a block program's Setup() in memory, with every turtle finishing
    /// its walk at once, and reports what the turtles drew. For tests and for
    /// checking a turtle challenge without waiting for the animation.
    /// Programs with music are not run this way (they'd start the sound engine).
    /// </summary>
    public static class HeadlessBlockRunner
    {
        public static HeadlessRunResult Run(BlockProgram program, int frames = 2)
        {
            var code = BlockCodeGenerator.Generate(program, instrument: true);
            var result = SketchCompiler.Compile(code.Source);
            if (!result.Success)
                return new HeadlessRunResult(false, result.Errors, Array.Empty<TurtleLine>());

            var sketch = result.Sketch!;
            try
            {
                return new HeadlessRunResult(true, Array.Empty<string>(), RunTurtles(sketch, frames));
            }
            finally
            {
                sketch.Dispose();
            }
        }

        /// <summary>Setup() and a few frames of an already compiled sketch, then everything its turtles drew.</summary>
        internal static IReadOnlyList<TurtleLine> RunTurtles(Sketch sketch, int frames)
        {
            using var surface = SKSurface.Create(new SKImageInfo(600, 400, SKColorType.Rgba8888, SKAlphaType.Premul));
            sketch.SetCanvas(surface.Canvas, surface);
            sketch.Setup();
            for (int i = 0; i < frames; i++)
            {
                surface.Canvas.Save();
                sketch.RenderFrame();
                surface.Canvas.Restore();
            }
            return FinishedLines(sketch);
        }

        /// <summary>Every line the sketch's turtles will have drawn once they finish walking.</summary>
        internal static IReadOnlyList<TurtleLine> FinishedLines(Sketch sketch)
        {
            Turtle[] turtles;
            lock (sketch.Turtles)
                turtles = sketch.Turtles.ToArray();
            foreach (var t in turtles)
                t.Finish();
            return turtles.SelectMany(t => t.Lines).ToList();
        }
    }
}
