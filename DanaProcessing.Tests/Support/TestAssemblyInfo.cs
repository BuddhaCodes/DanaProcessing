// The audio engine and a few other pieces are process-wide singletons:
// run test classes one after another instead of in parallel.
[assembly: Xunit.CollectionBehavior(DisableTestParallelization = true)]
