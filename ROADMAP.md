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

## Other directions worth a line, not yet scoped

Came up while thinking through #1 and #2 but aren't fleshed out — flagging so they don't get lost, not committing to them:

- **A sketch-sharing gallery**, OpenProcessing-style, even as something as light as a GitHub-backed community samples browser inside the IDE.
- **Compute-shader-leaning 3D** — GPU particle systems, GPU noise — pushing into TouchDesigner-adjacent territory Processing's P3D doesn't really compete in.

---

*This file exists so these ideas don't just live in a chat transcript. Update it as initiatives move between status labels, and add to "Other directions" freely — the bar for landing there is "worth remembering," not "worth committing to."*
