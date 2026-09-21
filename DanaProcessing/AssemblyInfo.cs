using System.Runtime.CompilerServices;

// The Avalonia host project needs to set engine-only state (MouseX, Key,
// FrameCount, etc.) that stays `internal` to everyone else — consumer apps
// embedding AvaloniaSketchCanvas should never be able to write these
// directly. This grants exactly that assembly the access that plain
// `internal` used to give for free back when everything lived in one
// project.
[assembly: InternalsVisibleTo("DanaProcessing.AvaloniaHost")]
