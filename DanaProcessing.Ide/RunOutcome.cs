namespace DanaProcessing.Ide
{
    /// <summary>What a press of Run actually did -- so callers that act on it
    /// (Agent mode's run_sketch) report the real result instead of guessing
    /// from state left behind by an earlier run.</summary>
    public enum RunOutcome
    {
        /// <summary>Compiled and loaded into the canvas; Setup()/Draw() run from the next frame.</summary>
        Started,
        /// <summary>The C# didn't compile; MainWindow.LastCompileErrors has the errors.</summary>
        CompileFailed,
        /// <summary>A `// nuget:` package couldn't be restored; nothing was compiled.</summary>
        RestoreFailed,
        /// <summary>It compiled but couldn't be started (e.g. its constructor threw).</summary>
        Failed,
        /// <summary>Another Run was already in progress; this one did nothing.</summary>
        Busy,
        /// <summary>No sketch text in the active tab.</summary>
        NothingToRun,
    }
}
