using System.Runtime.CompilerServices;

// The Avalonia host project needs to set engine-only state (MouseX, Key,
// FrameCount, etc.) that stays `internal` to everyone else — consumer apps
// embedding AvaloniaSketchCanvas should never be able to write these
// directly. This grants exactly that assembly the access that plain
// `internal` used to give for free back when everything lived in one
// project.
[assembly: InternalsVisibleTo("DanaProcessing.AvaloniaHost")]

// The test project exercises engine internals directly (offscreen rendering,
// the audio engine with a recording output, the reel parser's helpers).
[assembly: InternalsVisibleTo("DanaProcessing.Tests")]

// The IDE sets a few host-only things on the sketch it's about to run: the
// seed (🎲 / 🔒 / a PNG's seed) and [Tweak] values read back from a PNG.
[assembly: InternalsVisibleTo("DanaProcessing.Ide")]
