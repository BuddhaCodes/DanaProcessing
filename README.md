# DanaProcessing

**Creative coding in C#, the way Processing and p5.js sketchers already think — plus a real desktop IDE built just for it.**

[![Build and Release](https://github.com/BuddhaCodes/DanaProcessing/actions/workflows/build-release.yml/badge.svg)](https://github.com/BuddhaCodes/DanaProcessing/actions/workflows/build-release.yml)
[![.NET 8](https://img.shields.io/badge/.NET-8.0-512BD4)](https://dotnet.microsoft.com/)
[![Platform](https://img.shields.io/badge/platform-Windows-0078D6)](#getting-started)
[![License: MIT](https://img.shields.io/badge/license-MIT-brightgreen)](LICENSE.txt)

Write a `Sketch`, override `Setup()` and `Draw()`, and you already know the vocabulary: `Fill`, `Stroke`, `Rect`, `Translate`, `Rotate`, `PVector`, `Random`, `Noise`. Nothing to configure, no project boilerplate — just a class and two methods. What's different is everything *around* the sketch: a purpose-built IDE with live diagnostics, one-click NuGet packages, real 3D, and a "Run" button that gets you from idea to pixels in well under a second.

## A sketch is this small

```csharp
public class MySketch : Sketch
{
    private float angle;

    public override void Setup() => Size(800, 600);

    public override void Draw()
    {
        Background(20, 20, 30);
        Translate(Width / 2f, Height / 2f);
        Rotate(angle += 1f);
        Fill(255, 150, 90);
        NoStroke();
        Rect(-100, -100, 200, 200);
    }
}
```

Flip one word and the same mental model gets you into three dimensions — real geometry, real lighting, no separate engine to learn:

```csharp
public class MySketch : Sketch
{
    private float angle;

    public override void Setup() => Size(800, 600, RendererKind.Renderer3D);

    public override void Draw()
    {
        Background(20, 20, 30);
        Lights();
        Translate(Width / 2f, Height / 2f, 0);
        RotateY(angle += 0.02f);
        Fill(255, 150, 90);
        Box(200);
    }
}
```

## Why people stick around

- **One API, two renderers.** 2D runs on SkiaSharp; 3D runs on real OpenGL via Silk.NET, with lights, cameras, materials, custom shaders, and antialiasing you control from a settings panel — not two libraries pretending to be one.
- **An IDE that understands your sketch as you type it.** Live Roslyn diagnostics underline mistakes before you run anything; autocomplete knows the whole `Sketch` API; hitting **Run** compiles in memory and swaps the canvas in place — no project reload, no waiting.
- **NuGet packages, right inside the sketch.** Drop a `// nuget: PackageName, 1.2.3` comment at the top of your file and the IDE resolves, downloads, and links it before running — no `.csproj` to touch.
- **Ship it as a real app.** One export turns your sketch into a single, self-contained, double-clickable executable — no runtime install, nothing to explain to whoever you send it to.
- **28 example sketches, ready to open and break.** Bouncing circles, a recursive fractal tree, orbit cameras, faceted gems with hand-built normals, a reusable GPU-uploaded 3D swarm, binary counters, parallel prime search — each one commented to explain not just *what* it does but *why* it's built that way.
- **It speaks your language.** The whole interface — menus, dialogs, error messages, sample descriptions — is fully localized in English and Spanish, switchable from Settings.
- **Make it yours.** Colors, corner roundness, and fonts are all editable from an in-app Settings window, live-previewed as you tweak them.

## Getting started

**Just want to draw something?** Grab the latest build — a single `.exe`, nothing to install:

**[Download for Windows](https://github.com/BuddhaCodes/DanaProcessing/releases/latest/download/DanaProcessingIde-win-x64.zip)**

Unzip it, run `DanaProcessing.Ide.exe`, and press **Run** on the sketch that's already open. (Windows SmartScreen may flag it as from an unrecognized publisher on first launch — that's expected for an unsigned indie build; choose **More info → Run anyway**.)

**Building from source instead?** You'll need the [.NET 8 SDK](https://dotnet.microsoft.com/download):

```bash
git clone https://github.com/BuddhaCodes/DanaProcessing.git
cd DanaProcessing
dotnet run --project DanaProcessing.Ide
```

## What's in the repository

```text
DanaProcessing/                The core library — Sketch, Setup()/Draw(), 2D (SkiaSharp)
                                and 3D (Silk.NET/OpenGL) rendering, PVector, PImage,
                                PGraphics, PShader, collections, math, threading.

DanaProcessing.AvaloniaHost/    A cross-platform Avalonia control that hosts a running
                                Sketch — the embeddable canvas both the IDE and exported
                                standalone apps are built on.

DanaProcessing.Ide/             The desktop IDE itself: editor, live diagnostics,
                                NuGet management, sample gallery, exporter, settings,
                                and localization.
```

Full API reference lives in [`index.html`](index.html) — open it locally in a browser for the complete method-by-method documentation.

## Under the hood

| | |
| --- | --- |
| Language / runtime | C# on .NET 8 |
| UI framework | [Avalonia UI](https://avaloniaui.net/) |
| 2D rendering | [SkiaSharp](https://github.com/mono/SkiaSharp) |
| 3D rendering | [Silk.NET](https://github.com/dotnet/Silk.NET) over OpenGL 3.3 |
| In-IDE compilation | Roslyn (`Microsoft.CodeAnalysis`) — sketches compile in memory, no disk round-trip |
| Package resolution | `NuGet.Protocol`, resolved straight from `// nuget:` comments |

## Contributing

Issues and pull requests are welcome — whether that's a bug, a new example sketch, or an idea for the API. If you build something with DanaProcessing, open an issue and show it off.

## License

[MIT](LICENSE.txt) — do pretty much anything with it, just keep the notice.
