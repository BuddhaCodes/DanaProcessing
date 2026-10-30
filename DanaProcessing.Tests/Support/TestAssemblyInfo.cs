using System.Runtime.CompilerServices;

// The audio engine and a few other pieces are process-wide singletons:
// run test classes one after another instead of in parallel.
[assembly: Xunit.CollectionBehavior(DisableTestParallelization = true)]

namespace DanaProcessing.Tests.Support
{
    internal static class TestHostThreadPool
    {
        /// <summary>
        /// The test host itself parks thread-pool threads for the whole run:
        /// vstest's socket message loop polls synchronously on one, and the
        /// xUnit adapter blocks another waiting for the assembly to finish.
        /// On a 2-core machine that is the entire default minimum, so the
        /// timers live loops wake up on (Task.Delay) queue behind them until
        /// the pool injects a new thread, about 500 ms later -- long enough
        /// for a note to land late or not at all, which made
        /// AudioEngineTests.Live_loop_is_sample_accurate fail ~1 run in 4 on
        /// such machines (macOS CI runners have 3 cores). The IDE has no such
        /// blocked threads; this only gives the tests the headroom the real
        /// app already has. It only ever raises the minimum.
        /// </summary>
        [ModuleInitializer]
        internal static void EnsureHeadroom()
        {
            ThreadPool.GetMinThreads(out int worker, out int io);
            int wanted = Environment.ProcessorCount + 8;
            if (worker < wanted)
                ThreadPool.SetMinThreads(wanted, io);
        }
    }
}
