<p align="center">
  <img src="DanaProcessing.Ide/Theme/dana.png" alt="DanaProcessing logo" width="110">
</p>

<h1 align="center">DanaProcessing</h1>

<p align="center">
  <b>Creative coding in C#, the Processing way.</b><br>
  2D, real 3D and a live-coding audio engine, with a desktop IDE built just for sketching.
</p>

<p align="center">
  <a href="https://github.com/BuddhaCodes/DanaProcessing/actions/workflows/build-release.yml"><img src="https://github.com/BuddhaCodes/DanaProcessing/actions/workflows/build-release.yml/badge.svg" alt="Build and Release"></a>
  <a href="https://dotnet.microsoft.com/"><img src="https://img.shields.io/badge/.NET-10-512BD4" alt=".NET 10"></a>
  <a href="#-getting-started"><img src="https://img.shields.io/badge/platform-Windows%20%7C%20macOS%20%7C%20Linux-1B3C53" alt="Platforms"></a>
  <a href="LICENSE.txt"><img src="https://img.shields.io/badge/license-MIT-brightgreen" alt="License: MIT"></a>
  <a href="https://buddhacodes.github.io/DanaProcessing/"><img src="https://img.shields.io/badge/docs-online-B8863A" alt="Documentation"></a>
</p>

<p align="center">
  <a href="https://buddhacodes.github.io/DanaProcessing/"><b>Documentation</b></a> ·
  <a href="#-getting-started"><b>Download</b></a> ·
  <a href="#-live-coded-sound"><b>Sound</b></a> ·
  <a href="#-support-the-project"><b>Support</b></a>
</p>

<p align="center">
  <a href="https://buddhacodes.github.io/DanaProcessing/"><img src="og-image.png" alt="DanaProcessing: a live loop and a Draw() that reacts to the sound" width="760"></a>
</p>

<!-- A short GIF of the IDE (edit a sketch, press Run, see it change) would go great right here. -->

## ✨ Why it exists

I started programming with Java and JavaScript following [The Coding Train](https://www.youtube.com/@TheCodingTrain), sketching in [Processing](https://processing.org) and [p5.js](https://p5js.org). When I moved to C#, I wanted that same experience and couldn't find it, so I built it.

DanaProcessing borrows Processing's vocabulary on purpose: `Setup()`, `Draw()`, `Fill()`, `Rect()`, `Translate()`, `PVector`, `Noise()`. If you've written a Processing sketch, you can already read one of these. Then it adds what C# and .NET make possible: a real IDE, any NuGet package as a library, real 3D, and its own sound engine.

## 🎨 A sketch is this small

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

<details>
<summary><b>Same idea in 3D</b>: change one word</summary>

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

Real geometry, real lighting, no separate engine to learn.
</details>

## 🎵 Live-coded sound

A music engine inspired by [Sonic Pi](https://sonic-pi.net), written from scratch in C# and running inside the sketch, so what you hear and what you draw share one clock. Mark a method as a `[LiveLoop]` and it repeats in time with the others. Edit it and press **Run**: the music doesn't stop, and each loop picks up the new code on its next pass.

```csharp
[LiveLoop]
async Task Drums()
{
    Sample("kick");
    await Sleep(1);
    Sample("kick");
    Sample("snare");
    await Sleep(1);
}

[LiveLoop]
async Task Bass()
{
    UseSynth(Synth.Saw);
    Play("E2", release: 0.3, cutoff: 80);
    await Sleep(0.5);
}

public override void OnNote(NoteEvent e)
{
    if (e.Sample == "kick") flash = 1;   // fires the moment the kick is heard
}
```

Synths, drums, `.wav` samples, envelopes, filters, and `Amplitude` / `Spectrum()` to drive your visuals — of the sketch's own sound, or of the room with `ListenTo(AudioSource.Microphone)`. `Beat()`, `Phase()`, `Pulse()`, `OnBeat()` and `BeatTween(..., Ease.OutBack)` tie animation to the music's clock with no arithmetic, and MIDI controllers come in with `OnMidi()` / `MidiCC()` — or by MIDI learn, straight onto `[Tweak]` sliders. Sound plays through WinMM on Windows, CoreAudio on macOS and ALSA on Linux, all without extra packages.

## 🧰 What you get

| | |
| --- | --- |
| 🖥️ **An IDE that knows your sketch** | Live Roslyn diagnostics (listed in the Live errors tab once you pause typing), autocomplete for the whole API, go-to-definition, and a **Run** that compiles in memory in well under a second. |
| 🔥 **Hot reload that keeps state** | Tweak a value in a particle system you've been tuning for ten minutes and it keeps running, instead of starting over from `Setup()`. |
| 🧊 **One API, two renderers** | 2D on SkiaSharp, real 3D on OpenGL (Silk.NET) with lights, cameras, materials, custom shaders and 200,000 GPU particles. |
| 📦 **Any NuGet package as a library** | Put `// nuget: PackageName, 1.2.3` at the top of a sketch and it's downloaded and linked. No `.csproj` to touch. |
| 🌍 **Data from the web, maps and globes** | `LoadJSONObject("https://...")` like Processing, or `RequestGeoJSON(url, cacheFor: ...)` to keep drawing while it downloads (with progress and a disk cache). GeoJSON countries, distances and circles on the Earth, image pixels you can read, and a point cloud that draws tens of thousands of dots at once: enough for a live earthquake globe in one sketch. |
| 📡 **Live data** | `ConnectWebSocket(url)`, `ListenEvents(url)` (server-sent events), `ListenWebhook(port)` and `ConnectSignalR(hubUrl)`: feeds that fill in the background while `Draw()` reads them, reconnect by themselves and close with the sketch. SignalR is spoken directly, no packages, both ways: the *Inside Kestrel* sample draws a live ASP.NET Core app and breaks it from the keyboard. |
| 🔊 **Live-coded sound** | Live loops, synths, drums and samples, with visuals that react on the beat. |
| 🎛️ **Made to play live** | `[Tweak]` sliders you can MIDI-learn onto your controller's knobs, a performance mode with the code floating over the full-screen visuals (on the projector), and visuals locked to the beat. |
| 🎲 **Seeds you can keep** | Every Run has a seed; the PNGs a sketch saves carry it (and the slider values), a variations sheet draws 9 or 16 alternatives side by side, and print export redraws any of them as vectors — a 16x poster or a pen-plotter SVG. |
| ⏪ **A time machine** | Rewind the running sketch and continue from an earlier frame with its real state; every Run keeps its code and a picture, one click from coming back. |
| 📊 **F3** | A performance panel (fps, `Draw()` time, memory per frame, GCs) in the IDE and in exported apps. |
| 🗂️ **60+ examples, searchable** | From a minimal sketch to orbit cameras, ML.NET models that learn while you click, a TensorSharp neural network that paints, MIDI/OSC and a live-coded beat. Search by name, topic or a function in the code. |
| 🚀 **Ship it as an app** | Export any sketch as a single, self-contained, double-clickable executable. |
| 🌎 **English and Spanish** | The whole interface is localized and switchable from Settings. |

## 🚀 Getting started

**Just want to draw something?** Grab the installer for your system from the **[latest release](https://github.com/BuddhaCodes/DanaProcessing/releases/latest)**: `Setup.exe` for Windows, the `.pkg` for macOS (Apple Silicon or Intel) or the `.AppImage` for Linux. It includes everything it needs and updates itself when a new version is out.

The installers aren't code-signed yet (that costs real money on every platform), so your OS will ask if you're sure the first time. That's expected:

<details>
<summary><b>Windows</b>: SmartScreen</summary>

When Windows says "Windows protected your PC", click **More info → Run anyway**.
</details>

<details>
<summary><b>macOS</b>: Gatekeeper</summary>

Open the installer or the app once; macOS will say it can't verify the developer. Go to **System Settings → Privacy & Security**, click **Open Anyway**, then confirm **Open**. macOS remembers this afterwards.
</details>

<details>
<summary><b>Linux</b>: execute bit</summary>

```bash
chmod +x DanaProcessingIde-*.AppImage
./DanaProcessingIde-*.AppImage
```
</details>

Sound on macOS and Linux is new in 1.0 and still experimental: if you hear nothing, please [open an issue](https://github.com/BuddhaCodes/DanaProcessing/issues).

**Building from source?** You'll need the [.NET 10 SDK](https://dotnet.microsoft.com/download):

```bash
git clone https://github.com/BuddhaCodes/DanaProcessing.git
cd DanaProcessing
dotnet run --project DanaProcessing.Ide
```

## 📚 Documentation

The full reference, method by method, lives at **[buddhacodes.github.io/DanaProcessing](https://buddhacodes.github.io/DanaProcessing/)**: one page per topic (2D, 3D, sound, live, the IDE...), a search box (press <kbd>/</kbd>) and an [A–Z index](https://buddhacodes.github.io/DanaProcessing/reference/a-z.html). Open [`index.html`](index.html) to read it locally.

The pages are generated: edit a topic in [`site/content/`](site/content) (or the home page in `site/home.html`), then run `python site/build.py`. The look lives in `assets/site.css` and `assets/site.js`. Old links to the one-page site (`index.html#fill`) still land on the right page.

## 🏗️ What's in the repository

```text
DanaProcessing/                The core library: Sketch, Setup()/Draw(), 2D (SkiaSharp) and
                               3D (Silk.NET/OpenGL) rendering, the audio engine, PVector,
                               PImage, PGraphics, PShader, collections, math.

DanaProcessing.AvaloniaHost/   A cross-platform Avalonia control that hosts a running Sketch,
                               the canvas both the IDE and exported apps are built on.

DanaProcessing.Ide/            The desktop IDE: editor, live diagnostics, hot reload, NuGet
                               management, example gallery, exporter, settings, localization.
```

<details>
<summary><b>Under the hood</b></summary>

| | |
| --- | --- |
| Language / runtime | C# 14 on .NET 10 |
| UI framework | [Avalonia UI](https://avaloniaui.net/) |
| 2D rendering | [SkiaSharp](https://github.com/mono/SkiaSharp) |
| 3D rendering | [Silk.NET](https://github.com/dotnet/Silk.NET) over OpenGL |
| Audio | Own engine in C#: logical-time scheduler, allocation-free audio thread, `winmm` output on Windows |
| In-IDE compilation | Roslyn (`Microsoft.CodeAnalysis`), in memory, no disk round-trip |
| Package resolution | `NuGet.Protocol`, straight from `// nuget:` comments |
</details>

## 🗺️ Roadmap

Where it's headed is in [`ROADMAP.md`](ROADMAP.md): hot reload on save, cross-platform builds, GPU particle systems and more.

## 💛 Support the project

DanaProcessing is free, open source, and built in spare time. If it's useful to you, for your art, your classes or your installations, you can help it keep growing with a donation via **[PayPal](https://paypal.me/CarlosFernandez934)**. Every contribution turns into more time for new features, examples and fixes.

> **A note on where donations go:** PayPal isn't available where I live, so donations are received on my behalf by a trusted collaborator, **Carlos Fernandez**. That's the name you'll see on the PayPal page.

*¿Hablás español? DanaProcessing es gratis y de código abierto; si te sirve, podés apoyarlo con una donación por [PayPal](https://paypal.me/CarlosFernandez934). Como PayPal no está disponible donde vivo, las donaciones las recibe en mi nombre un colaborador de confianza, Carlos Fernandez.*

You'll also find the link inside the IDE under **Help → Support the project**.

## 🤝 Contributing

Issues and pull requests are welcome: a bug, a new example sketch, or an idea for the API. If you build something with DanaProcessing, open an issue and show it off. I'd love to see it.

## 🙏 Inspiration

This project stands on the shoulders of [Processing](https://processing.org) and [p5.js](https://p5js.org), whose API it deliberately mirrors (some examples are adaptations of p5.js classics); [The Coding Train](https://www.youtube.com/@TheCodingTrain), where I learned to sketch; and [Sonic Pi](https://sonic-pi.net), whose way of live coding music shaped the audio engine.

## 📄 License

[MIT](LICENSE.txt). Do pretty much anything with it, just keep the notice.
