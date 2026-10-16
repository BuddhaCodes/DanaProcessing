# Roadmap

DanaProcessing v0.1 beta is a solid Processing port: the same `Setup()`/`Draw()` model, in both 2D and real 3D, inside an IDE that's already ahead of Processing's own (live diagnostics, in-sketch NuGet, one-click export). That's the foundation, not the pitch. The two initiatives below are what would give the project its own identity instead of staying "Processing, but C#" — each is a genuine capability Processing/p5.js can't easily match, not just a nicer version of something they already have.

Status labels: **Idea** (scoped, not started) · **In progress** · **Shipped**.

---

## 1. "Any NuGet package is a sketch library" — Shipped

**The pitch.** Installing a library in real Processing means the Library Manager, a restart, and hoping the jar plays nice. In DanaProcessing it's a comment line — `// nuget: PackageName, 1.2.3` — already fully working (search, resolve, cache, link, all before Run compiles the sketch). That's not a nice-to-have, it's structural: the entire .NET/NuGet ecosystem is a sketch library, today, with zero extra engine work. Nobody knows that yet because nothing has shown them what it's for.

**What "done" looks like.** A handful of flagship example sketches — real, working, in the sample gallery — that couldn't exist as one-comment integrations in Processing or p5.js. All three shipped:

- ~~**NAudio** — low-latency audio-reactive visuals.~~ **Shipped**: "Audio-reactive: live FFT (NAudio)" — `WasapiLoopbackCapture` + a real FFT driving 40 bars off whatever's playing on the system.
- ~~**MIDI/OSC** (`Sanford.Multimedia.Midi` → switched to `NAudio.Midi`, `Rug.Osc`) — an installation/live-performance-style sketch.~~ **Shipped**: "Reactive installation: MIDI + OSC" — NAudio.Midi notes/CC and Rug.Osc UDP messages both driving one ripple grid, click-to-test when neither is connected.
- ~~**ML.NET / ONNX Runtime** — an on-device generative-art or style-transfer sketch.~~ **Shipped**: "ML.NET color field: live learning" — a real `Microsoft.ML` regression model, trained live (no external model file) on colored seed points you click in, painting a generative field around them. Found and fixed a real trap along the way: SDCA's default trainer is open-ended convergence-based and measured 40+ seconds on this sketch's tiny/sparse data shape with no predictable pattern — capping `MaximumNumberOfIterations` explicitly brought every run back to single-digit milliseconds.

**Why this order.** Each example is self-contained and additive — no engine changes, no breaking anything already shipped. The risk is entirely "does anyone see it," not "does it work." Natural follow-up now that all three have landed: a short post/video per example, since "look what one comment gets you" is the whole argument and it's a visual one.

---

## 2. True hot-reload — In progress

**The pitch.** Pressing Run today does exactly what Processing's own Run does: recompile and start over from a blank `Setup()`. For anything performance- or exploration-oriented — a particle system you've been tuning for ten minutes, a generative piece mid-evolution — that's ten minutes gone every time you tweak a number. Edit-in-place, keep the state running, is a real live-coding feature (think Smalltalk-image or a shader live-editor), not something Processing or p5.js's own editors offer.

**Why it's a bigger bet than #1.** This isn't examples — it's a real engineering problem. A fresh `Run` produces a *new compiled assembly* with a *new* `Sketch` instance; hot-reload means recompiling while somehow carrying forward the fields of the *old* instance (positions, velocities, accumulated frame count, whatever the sketch's own state is) into the new one. Rough shape of what that needs:

- Detecting which fields exist on both the old and new compiled `Sketch` types and copying matching ones across (reflection-based field copy is the obvious first cut; doesn't handle a renamed/retyped field gracefully, which needs its own fallback — probably "just reset," with a clear message, rather than a silent wrong value).
- Deciding what "matching" means when a field's *type* changed, not just added/removed fields.
- Triggering on file save (a `FileSystemWatcher` on the active tab, or reusing whatever the editor's live-diagnostics pass already watches) rather than requiring an explicit second button, so it actually feels like live coding instead of "Run but faster."

**Suggested scope for a first cut.** Don't try to handle every edge case at once — ship it for the common case (same fields, same types, sketch didn't add/remove a field) with a visible, honest fallback (full restart + a status-bar note) for anything the reflection-based copy can't confidently handle. Better to be clear about the boundary than to guess wrong silently.

**First cut shipped.** A ⚡ button next to Run does exactly the above — same-fields-same-types reflection copy (`SketchHotReload` in the core project), honest all-or-nothing fallback to a full restart with the mismatch reason shown, enabled only while the tab you're looking at is the one actually running. Deliberately an *explicit* button rather than triggering on file save — the automatic, "feels like live coding" version above is still a real next step, just a separate, bigger UX call (does every pause-while-typing recompile? what if you're mid-edit and don't want the visual to change yet?) that's worth its own decision later rather than bundling into the first cut.

---

## 3. Cross-platform export (macOS + Linux) — In progress

**The pitch.** The engine (Avalonia + Silk.NET) was already cross-platform — confirmed by grep, exactly two `OperatingSystem.IsWindows()`-gated spots in the whole codebase, both already cleanly conditional. What was actually Windows-only was narrower: the CI release pipeline only built `win-x64`, and the "Export sketch as standalone app" feature hardcoded `null` for anything but Windows.

**What shipped.** `SketchExporter.GetCurrentRid()` now returns the right RID (`win-x64/x86/arm64`, `osx-x64/arm64`, `linux-x64/arm64`) for whichever OS + architecture is actually running, instead of hardcoding Windows. `build-release.yml` was restructured into a `read-version` → `{build-windows, build-macos, build-linux}` → `release` job graph: macOS builds a real unsigned `.app` bundle (via a real `macos-latest` runner, since `chmod +x` needs a Unix machine to stick) for both `osx-x64` and `osx-arm64`, Linux ships a `chmod +x`-and-run tarball with an optional `.desktop` launcher entry, and the final `release` job attaches all four platform artifacts to one GitHub Release instead of racing four concurrent release calls. Unsigned on every platform (same budget call already made for Windows) — README now documents the macOS Gatekeeper "Open Anyway" path and the Linux execute-bit step alongside the existing Windows SmartScreen paragraph.

**What's still unverified.** Real cross-compiled `dotnet publish` runs for all three non-Windows RIDs were run right here on this Windows machine and produced the expected output shape (single-file executable + correct native `.so`/`.dylib` deps) — but a real GitHub Actions run of the restructured workflow hasn't happened yet, so the `.app`-bundle assembly step (which needs `ditto`/`chmod` from a real macOS runner) is still unverified end-to-end, and nobody's actually launched the app on real macOS or Linux hardware.

---

## 4. Compute-shader-leaning 3D: GPU particle systems — In progress

**The pitch.** GPU particle systems and GPU noise, pushing into TouchDesigner-adjacent territory Processing's P3D doesn't really compete in — `CreateGpuParticles()` lets a sketch hand the engine a GLSL simulation snippet and get hundreds of thousands of particles simulated entirely on the GPU, every frame.

**A real constraint, discovered and designed around rather than ignored.** The engine's GL context was hardcoded to OpenGL 3.3 core; real GLSL compute shaders need 4.3+. Verified via web search: **macOS's native OpenGL implementation is hard-capped at 4.1** (Apple deprecated OpenGL after 10.14 and never shipped past it), so compute shaders are categorically unavailable there through OpenGL — a real problem since macOS export just shipped (#3, above). Rather than dropping the feature on macOS or giving up on "real" compute shaders everywhere, this ships a hybrid: `Renderer3DBackend.Create()` now tries a GL 4.3 core context first, verifying what it actually got (not just trusting the request — GLFW's Cocoa backend silently downgrades rather than throwing), and falls back to a classic ping-pong-texture GPGPU simulation when 4.3 isn't genuinely available. One sketch-facing API (`CreateGpuParticles()`/`StepParticles()`/`DrawParticles()`) either way — the exact same simulation snippet compiles into a real compute shader on Windows/Linux or a fragment-shader ping-pong pass on macOS, with zero branching in sketch code.

**What shipped.** Two-layer capability detection (version query + an actual throwaway compute-shader compile, since a driver can misreport its version) in `Renderer3DBackend.cs`; a full compute-shader backend (SSBO + `glDispatchCompute` + a memory barrier, `gl_VertexID`-indexed rendering with no instancing); a full ping-pong-texture fallback backend (RGBA32F position/velocity texture pairs, MRT FBOs, a shared fullscreen-quad simulation pass); a `Renderer3DSettings.ForceComputeFallback` debug switch so the fallback path is genuinely exercised on dev hardware that supports real compute shaders, not just code-reviewed; a new flagship sample ("200,000 GPU particles: curl noise") with an honest on-screen label saying which backend is actually running.

**Verified for real, not just compiled.** All of it — not just "it builds." A headless test harness drove real `Sketch` instances through the real `SketchCompiler`/`RenderFrame()` pipeline and inspected actual rendered pixels: both backends independently confirmed to render correct particle motion (the fallback forced on via `ForceComputeFallback` on this same real-4.3-capable machine, since the true macOS-unavailable trigger can't happen here); the throw-vs-silent-downgrade question settled empirically (a genuinely unavailable GL version throws a real `GlfwException` on this machine's WGL driver); all 15 3D samples in the gallery (14 pre-existing + the new one) re-verified to still compile and render with zero regression after the capability-detection rewrite; 200,000 particles measured at ~6ms/frame on both backends.

**What's still unverified** (the same honest gap #3 already carries): the real macOS-unavailable trigger, real Linux driver diversity, and an actual GitHub Actions run exercising this on real macOS/Linux hardware — none of that is reachable from this machine.

---

## 5. Version 1.0 — Release candidate (1.0.0-rc.1)

**The pitch.** Everything above made DanaProcessing interesting; 1.0 is about making it dependable: a frozen API, tests that run on every OS, installers that update themselves, and docs a newcomer can finish in half an hour.

**What landed** (branch `release/1.0`):

- **.NET 10 and a single SkiaSharp 3** across core, host and IDE (C# 14 in sketches). Svg.Skia replaces the old SkiaSharp.Svg.
- **API freeze.** `Rotate()` takes radians like Processing, with `RotateDegrees()` beside it; `FillHSB`/`StrokeHSB` take an alpha and wrap the hue; `PVector` has `+ - * /` operators; setters that sketches shouldn't touch are `internal`.
- **A real test suite + CI on Windows, macOS and Linux** (`ci.yml`): reels (parsing, rendering, GIF/PNG/MP4 export, typos, zoom, thumbnail), audio (engine, live loops, offline rendering), the compiler, hot reload, crash info, performance budgets, every sample and every runnable example in the docs compiling. Each CI run uploads a GIF, MP4s and a thumbnail rendered on each OS for checking by eye.
- **Sound on macOS and Linux** through CoreAudio (AudioQueue) and ALSA, no extra dependencies. Marked *experimental* until someone runs it on real hardware.
- **Installer and updates end to end** with Velopack (`build-release.yml`, unsigned for 1.0; see `docs/RELEASING.md`).
- **Friendly runtime errors**: a crash in `Draw()` marks the line in the editor and says what happened in plain words.
- **No memory leak on Run**: each compiled sketch lives in a collectible load context; `docs/PERFORMANCE.md` has the baselines CI enforces.
- **Reels, polished**: sound in the MP4 (live loops rendered in video time), typos with backspace, zoom on marked lines, a YouTube thumbnail.
- **Docs**: a 30-minute tutorial, a Processing/p5.js guide with an equivalence table, and a runnable example ending every API section.
- **Samples**: kinetic type, images and pixels, frame-by-frame animation (new topics and tags), plus the 10-episode YouTube intro series.
- **Editor**: Edit menu, find/replace, go to line (Ctrl+G), format document (Shift+Alt+F), open tabs remembered between sessions.

**What's still unverified** (needs the first CI run after pushing, then people): the three CI jobs themselves, macOS/Linux sound on real hardware, the macOS `.app` and Linux AppImage installed and updated end to end, and the update path from the previous version.

**Still to decide before 1.0 final**: which older samples to retire. Candidates, each mostly covered by another sample: "normal() (Silk.NET)" (the faceted gem shows Normal() better), "3D sphere" (lights and material already draw spheres), "Minimal 3D sketch" next to "3D cube", "Binary counter" and "Low-power clock: delay()" (single-function demos that could become runnable examples in the docs instead), and whether to sign the installers (SignPath is free for open source).

---

## 6. Tweak mode — In progress

**The pitch.** Processing had a "Tweak mode"; DanaProcessing can go further because hot reload already keeps state. Mark a field with `[Tweak(0, 10)]` and a slider panel appears over the running sketch: drag it and the value changes live, no recompile, no restart. It's the fastest possible loop for tuning a generative piece, and it looks great in a Reel.

**First cut — written, not yet compiled.** Code in `TweakAttribute.cs`, `TweakPanel.cs`, `Sketch.Tweak.cs`, hooks in `Sketch.RenderFrame()` and the `Raise*` mouse methods, tests in `TweakTests.cs`, sample "Tweak mode: live sliders with [Tweak]", docs entry `#tweak`. Scope: `[Tweak]` on `float`/`int`/`bool`/`Color` fields of the sketch; an overlay panel drawn by the engine after `Draw()` (click its header to fold it, `HideTweaks()`/`ShowTweaks()` from code), handling the mouse before the sketch sees it so dragging a slider doesn't also paint on the canvas. Values live in the sketch's own fields, so ⚡ Hot Reload keeps them for free.

**Second step — written, not yet compiled: back into the code.** *Sketch → Apply tweaks to code* writes the moved values into the fields' initializers (`TweakSourceRewriter` in the IDE, Roslyn-based, tests in `TweakSourceRewriterTests.cs`). Only sliders you actually moved count (the panel tracks them, across hot reloads too), and an initializer that already evaluates to the running value is left as written — so `1.5708f` never turns into `1.571f` and `MathF.PI / 2` survives untouched. One document update: a single Ctrl+Z undoes it.

**Next.** Drag numbers straight in the editor (Roslyn knows where each literal is), no `[Tweak]` needed; remember panel position per sketch.

---

## 7. Ideas backlog, first pass — written, not yet compiled

Everything here was written against the real code and APIs (checked in their sources), syntax-checked, but **not compiled or run** — the environment it was written in had no .NET SDK or NuGet access. Build, run the tests, then try each by hand.

- **An agent that sees the canvas.** New `look_at_canvas` tool: the canvas encodes its next finished frame as a JPEG (max 768 px) on the render thread (`AvaloniaSketchCanvas.CaptureFrameAsync`); Anthropic gets it as an image block inside the tool_result, OpenAI (text-only tool messages) as a user message right after. The system prompt now asks the agent to look before calling anything visual done.
- **Rewind.** `FrameHistory` keeps the last frames at logical size (128 MB budget, up to 600 frames); the canvas can `Freeze()`/`StepBack()`/`StepForward()`/`ScrubTo()` — frozen means the sketch doesn't run, and stepping forward from the newest frame runs exactly one real `Draw()`. `TimelineBar` under the canvas drives it. Visual only: the sketch's state isn't rolled back (re-running from an earlier frame would need state snapshots — next step).
- **`OnStop()`** (Processing's `dispose()`): called once when a sketch stops for good (another Run, its window closing), never on Hot Reload. Devices opened by a sketch used to stay busy until the IDE closed.
- **`// nuget:` now loads `runtimes/`.** The resolver extracts `runtimes/<rid>/lib/<tfm>` (replacing lib/ stubs, e.g. System.IO.Ports) and `runtimes/<rid>/native` (most specific RID first); `SketchCompiler` resolves native libraries from there via `ResolvingUnmanagedDll` (and PATH on Windows); exports copy them next to the executable. Cache marker bumped to `.complete-v2` so old cached packages re-extract once. Not handled yet: `runtime.json`-style native packages (Linux/macOS native part of System.IO.Ports).
- **Physical world samples:** Arduino over serial (System.IO.Ports), webcam with slit-scan (FlashCap + new `LoadImage(byte[])`), MIDI out bouncing notes (NAudio). New sample tag *Hardware*.
- **Live shaders:** `Shade(sksl)` / `PShader2D` on SkiaSharp's `SKRuntimeEffect` — Shadertoy-style uniforms (iTime, iResolution, iMouse, iFrame, iAmplitude, iSpectrum[]), 2D renderer, every OS, cached by source so Hot Reload swaps shaders live; a compile error keeps the last good shader and shows the message. Sample "Live shaders: a plasma that hears the beat".
- New samples live in `SketchSamples.Extras.cs`; `SketchSamples.All` concatenates.
- **Record your interaction for a reel.** *Sketch → Record interaction for the reel* records mouse paths, clicks, drags and keys over the running sketch (`ReelInputRecorder`, up to 30 s) and, on stop, writes them as `// @reel move/drag/click/key` markers at the top of the tab -- the reel's result part replays what you actually did. Round-trip tested through `ReelScript.Parse`.
- **Guided challenges — postponed.** A first cut (offscreen checks over what the sketch draws) was written and taken out to revisit later.
- **Live preview in Examples.** Selecting a sample compiles it off the UI thread and runs it scaled in the details pane (`SamplePreview`); samples with sound, `// nuget:` or 3D show a note instead, so browsing never plays music, downloads or creates GL contexts. (This replaces "animated thumbnails": one live preview instead of 60 tiny ones.)
- **Web export — dropped.** A web player (Avalonia.Browser + WebAssembly, loading the IDE-compiled sketch DLL) was written and then removed by decision: not pursuing browser export for now.

---

## Other directions worth a line, not yet scoped

Came up while thinking through the initiatives above but aren't fleshed out — flagging so they don't get lost, not committing to them:

- **A sketch-sharing gallery**, OpenProcessing-style, even as something as light as a GitHub-backed community samples browser inside the IDE.
- **Rewind / time scrubbing.** `VirtualMillis` already makes the clock controllable; a ring buffer of the last N frames would let you pause, scrub back, step frame by frame and "re-run from frame 300 with this change". A debugger for creative coding.
- **Live shader sketches (Shadertoy-style).** A sketch kind that's just a fragment shader, with `time`, `mouse`, resolution and the **audio spectrum** (as a texture) wired up automatically, recompiled on save. Ties the sound engine to the GPU.
- **An agent that can see the canvas.** The IDE agent already knows the API; give it a rendered frame of the running sketch (the Reels pipeline already renders offscreen) so it can act on "make it feel like a sunset" or "why is nothing showing up?" and check its own result.
- **Web export (WebAssembly).** Today a sketch exports as a desktop executable; a link that opens in the browser (SkiaSharp runs on WASM) is what would make the sharing gallery above really work. Hard, but the biggest lever for spreading the project.
- **Record a session as a Reel.** A record button that captures your real keystrokes plus the canvas while you code, exported as a Reel/MP4 — the YouTube videos make themselves.
- **Guided challenges**, Coding Train "Coding Challenge"-style: the IDE sets a goal and checks whether the sketch meets it. For the educational side and the YouTube series.
- **Webcam capture.** No video input exists yet — a real gap vs. Processing's Video library. Combined with TensorSharp/ONNX it opens hand/face tracking from one `// nuget:` line.
- **Ableton Link + MIDI out.** Sync `[LiveLoop]`s to other musicians' tempo and drive external synths — makes the sound engine a live-performance tool.
- **Serial / Arduino** with a tiny API (`Serial.Read()`), the Processing classic for installations.
- **Small ones:** animated thumbnails in the Samples window (rendered with the Reels code).

---

*This file exists so these ideas don't just live in a chat transcript. Update it as initiatives move between status labels, and add to "Other directions" freely — the bar for landing there is "worth remembering," not "worth committing to."*
