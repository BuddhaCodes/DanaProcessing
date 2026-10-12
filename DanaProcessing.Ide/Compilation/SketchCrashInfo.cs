using System;
using System.Diagnostics;
using System.Reflection;
using System.Text.RegularExpressions;

namespace DanaProcessing.Ide.Compilation
{
    /// <summary>Where in the user's code a running sketch failed, if it was in the user's code at all.</summary>
    public sealed record SketchCrashInfo(int Line, string ExceptionName, string Message, string Context)
    {
        private static readonly Regex LineInTrace = new(@"Sketch\.cs:line (\d+)", RegexOptions.Compiled);

        /// <summary>
        /// Finds the innermost frame of the sketch's own code (compiled as
        /// SketchCompiler.SketchFileName with a PDB) in the exception's stack
        /// trace. Line is 0 when the failure has no frame in the sketch -- e.g.
        /// a bad argument detected deep inside the library.
        /// </summary>
        public static SketchCrashInfo From(Exception ex, string context)
        {
            while (ex is TargetInvocationException { InnerException: { } inner })
                ex = inner;
            if (ex is AggregateException { InnerExceptions.Count: 1 } agg)
                ex = agg.InnerExceptions[0];

            int line = 0;
            try
            {
                foreach (var frame in new StackTrace(ex, true).GetFrames())
                {
                    if (frame.GetFileName() is { } file && file.EndsWith(SketchCompiler.SketchFileName, StringComparison.OrdinalIgnoreCase) && frame.GetFileLineNumber() > 0)
                    {
                        line = frame.GetFileLineNumber();
                        break;
                    }
                }
            }
            catch
            {
                // Some runtimes can't resolve frames for in-memory assemblies; fall back to the text.
            }
            if (line == 0 && ex.StackTrace is { } trace && LineInTrace.Match(trace) is { Success: true } m)
                line = int.Parse(m.Groups[1].Value);

            return new SketchCrashInfo(line, ex.GetType().Name, ex.Message, context);
        }
    }
}
