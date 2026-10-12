# Performance baselines

`DanaProcessing.Tests/Core/PerformanceTests.cs` measures a few numbers on
every CI run and prints them in the test log (Actions → CI → a test job →
"Test" step). The limits in those tests are deliberately loose -- they catch
regressions, not noise -- so read the printed numbers, not just pass/fail.

Run them locally with:

```
dotnet test DanaProcessing.Tests -c Release --filter "FullyQualifiedName~PerformanceTests" --logger "console;verbosity=detailed"
```

| What | How it's measured | Target |
|---|---|---|
| Compile on Run | `SketchCompiler.Compile()` of a small sketch, first call and the average of 5 more | warm compile well under 1 s on a desktop |
| 2D drawing | 60 frames of 1 000 and 5 000 HSB circles, offscreen, 800x600 | 1 000 circles inside the 16.7 ms of a 60 fps frame |
| Memory over many Runs | 25 compile+run cycles, then a GC: how many sketch assemblies are still loaded | at most 6 (the last few are kept on purpose) |
| Audio thread | bytes allocated per render block while live loops play (`AudioEngineTests`) | exactly 0 |

## Memory: why sketches no longer leak

Each Run compiles the sketch into a new assembly. Until 1.0 those were loaded
into the IDE's default load context, which can never unload anything -- a
long session of Run, Run, Run kept every version in memory. Sketches now load
into their own collectible `AssemblyLoadContext`; the last four stay, older
ones are released as soon as nothing references them
(`SketchCompiler.LoadedSketchAssemblies` reports how many are alive).
