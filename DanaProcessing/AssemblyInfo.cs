using System.Runtime.CompilerServices;

// The WPF and Avalonia host projects need to set engine-only state (MouseX,
// Key, FrameCount, etc.) that stays `internal` to everyone else — consumer
// apps embedding SketchCanvas/AvaloniaSketchCanvas should never be able to
// write these directly. This grants exactly those two assemblies the access
// that plain `internal` used to give for free back when everything lived in
// one project.
[assembly: InternalsVisibleTo("DanaProcessing.Wpf")]
[assembly: InternalsVisibleTo("DanaProcessing.AvaloniaHost")]
