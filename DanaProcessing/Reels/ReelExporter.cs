using System.Diagnostics;
using System.Runtime.InteropServices;
using SkiaSharp;

namespace DanaProcessing.Reels
{
    public enum ReelExportKind
    {
        /// <summary>H.264 MP4 at full resolution -- what Instagram/TikTok/YouTube want. Needs ffmpeg.</summary>
        Mp4,
        /// <summary>Animated GIF at reduced size, written by DanaProcessing itself (no ffmpeg needed).</summary>
        Gif,
        /// <summary>One PNG per frame in a folder, for your own editor.</summary>
        Png,
    }

    public sealed class ReelExportOptions
    {
        public ReelExportKind Kind { get; set; } = ReelExportKind.Mp4;
        /// <summary>File (.mp4/.gif) or, for Png, a folder.</summary>
        public string OutputPath { get; set; } = "";
        /// <summary>Path to ffmpeg; null = look for it (see <see cref="ReelExporter.FindFfmpeg"/>).</summary>
        public string? FfmpegPath { get; set; }
        /// <summary>GIF width in pixels (height follows the format).</summary>
        public int GifWidth { get; set; } = 540;
        public int GifFps { get; set; } = 15;
        /// <summary>H.264 quality: lower = better and bigger. 18 is visually lossless for code.</summary>
        public int Crf { get; set; } = 18;
    }

    /// <summary>
    /// Renders a reel frame by frame -- in video time, not real time, so a
    /// heavy sketch still comes out smooth -- and writes it as MP4 (through
    /// ffmpeg), GIF (own encoder) or a PNG sequence.
    /// </summary>
    public static class ReelExporter
    {
        /// <summary>
        /// Finds ffmpeg: <paramref name="preferred"/> if it exists, then the
        /// DANA_FFMPEG environment variable, then PATH, then the usual install
        /// places (winget, Chocolatey, Scoop, Homebrew). Null when not found.
        /// </summary>
        public static string? FindFfmpeg(string? preferred = null)
        {
            bool win = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
            string exe = win ? "ffmpeg.exe" : "ffmpeg";

            IEnumerable<string?> Candidates()
            {
                yield return preferred;
                yield return Environment.GetEnvironmentVariable("DANA_FFMPEG");
                foreach (var dir in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator))
                    if (!string.IsNullOrWhiteSpace(dir))
                        yield return Path.Combine(dir.Trim('"'), exe);

                var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                if (win)
                {
                    yield return Path.Combine(local, "Microsoft", "WinGet", "Links", exe);
                    yield return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "chocolatey", "bin", exe);
                    yield return Path.Combine(home, "scoop", "shims", exe);
                    yield return @"C:\ffmpeg\bin\ffmpeg.exe";
                    yield return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "ffmpeg", "bin", exe);

                    // winget's real install folder, in case the Links shim is missing.
                    var pkgs = Path.Combine(local, "Microsoft", "WinGet", "Packages");
                    if (Directory.Exists(pkgs))
                    {
                        foreach (var d in SafeDirs(pkgs, "*FFmpeg*"))
                            foreach (var bin in SafeDirs(d, "*"))
                                yield return Path.Combine(bin, "bin", exe);
                    }
                }
                else
                {
                    yield return "/opt/homebrew/bin/ffmpeg";
                    yield return "/usr/local/bin/ffmpeg";
                    yield return "/usr/bin/ffmpeg";
                }
            }

            foreach (var c in Candidates())
            {
                if (!string.IsNullOrWhiteSpace(c) && File.Exists(c))
                    return c;
            }
            return null;
        }

        private static IEnumerable<string> SafeDirs(string root, string pattern)
        {
            try { return Directory.GetDirectories(root, pattern); }
            catch { return Array.Empty<string>(); }
        }

        /// <summary>
        /// Renders and writes the reel. <paramref name="sketchFactory"/> must
        /// return a NEW instance of the sketch each call (the result part
        /// starts from a fresh one). Progress is 0..1. Runs on a worker thread.
        /// </summary>
        public static Task ExportAsync(ReelScript script, Func<Sketch>? sketchFactory, ReelExportOptions options,
                                       IProgress<double>? progress = null, CancellationToken cancel = default)
            => Task.Run(() => Export(script, sketchFactory, options, progress, cancel), cancel);

        private static void Export(ReelScript script, Func<Sketch>? sketchFactory, ReelExportOptions o, IProgress<double>? progress, CancellationToken cancel)
        {
            if (string.IsNullOrWhiteSpace(o.OutputPath))
                throw new ArgumentException("OutputPath is empty.");

            Reel.BeginExport();
            try
            {
                using var timeline = ReelTimeline.Build(script, sketchFactory);
                if (o.Kind == ReelExportKind.Gif)
                    timeline.AnimateBackground = false;
                switch (o.Kind)
                {
                    case ReelExportKind.Mp4: ExportMp4(timeline, o, progress, cancel); break;
                    case ReelExportKind.Gif: ExportGif(timeline, o, progress, cancel); break;
                    default: ExportPng(timeline, o, progress, cancel); break;
                }
                progress?.Report(1);
            }
            finally
            {
                Reel.EndExport();
            }
        }

        private static int FrameCount(ReelTimeline t, int fps) => Math.Max(1, (int)Math.Ceiling(t.Duration * fps - 1e-6));

        private static SKSurface NewSurface(int w, int h)
            => SKSurface.Create(new SKImageInfo(w, h, SKColorType.Rgba8888, SKAlphaType.Premul))
               ?? throw new InvalidOperationException($"Could not create a {w}x{h} drawing surface.");

        private static void RenderInto(SKSurface surface, ReelTimeline timeline, double t, float scale)
        {
            var c = surface.Canvas;
            c.Clear(SKColors.Black);
            c.Save();
            if (scale != 1) c.Scale(scale);
            timeline.Render(c, t);
            c.Restore();
            c.Flush();
        }

        /// <summary>Copies the surface's pixels into <paramref name="dest"/> as tightly packed RGBA.</summary>
        private static unsafe void ReadPixels(SKSurface surface, byte[] dest, int w, int h)
        {
            // ReadPixels(IntPtr) exists unchanged in SkiaSharp 2.88 and 3.x
            // (GetPixelSpan() changed its return type between them).
            var info = new SKImageInfo(w, h, SKColorType.Rgba8888, SKAlphaType.Premul);
            fixed (byte* p = dest)
            {
                if (!surface.ReadPixels(info, (IntPtr)p, w * 4, 0, 0))
                    throw new InvalidOperationException("Could not read the rendered frame.");
            }
        }

        private static void ExportMp4(ReelTimeline timeline, ReelExportOptions o, IProgress<double>? progress, CancellationToken cancel)
        {
            var ffmpeg = FindFfmpeg(o.FfmpegPath)
                ?? throw new FileNotFoundException("ffmpeg was not found. Install it (Windows: winget install Gyan.FFmpeg) or pick ffmpeg.exe, or export as GIF instead.");

            int w = timeline.Width, h = timeline.Height, fps = timeline.Fps;
            int frames = FrameCount(timeline, fps);

            var psi = new ProcessStartInfo(ffmpeg)
            {
                UseShellExecute = false,
                RedirectStandardInput = true,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                CreateNoWindow = true,
            };
            foreach (var a in new[]
            {
                "-y", "-hide_banner", "-loglevel", "error",
                "-f", "rawvideo", "-pix_fmt", "rgba", "-s", $"{w}x{h}", "-r", fps.ToString(), "-i", "-",
            })
                psi.ArgumentList.Add(a);
            foreach (var a in H264Args(ffmpeg, o.Crf))
                psi.ArgumentList.Add(a);
            foreach (var a in new[] { "-pix_fmt", "yuv420p", "-movflags", "+faststart", o.OutputPath })
                psi.ArgumentList.Add(a);

            using var proc = Process.Start(psi) ?? throw new InvalidOperationException("Could not start ffmpeg.");
            var errors = new Queue<string>();
            proc.ErrorDataReceived += (_, e) =>
            {
                if (e.Data == null) return;
                lock (errors)
                {
                    errors.Enqueue(e.Data);
                    while (errors.Count > 20) errors.Dequeue();
                }
            };
            proc.OutputDataReceived += (_, _) => { };
            proc.BeginErrorReadLine();
            proc.BeginOutputReadLine();

            var stdin = proc.StandardInput.BaseStream;
            var buffer = new byte[w * h * 4];
            bool ok = false;
            try
            {
                using var surface = NewSurface(w, h);
                for (int i = 0; i < frames; i++)
                {
                    cancel.ThrowIfCancellationRequested();
                    RenderInto(surface, timeline, i / (double)fps, 1);
                    ReadPixels(surface, buffer, w, h);
                    stdin.Write(buffer, 0, buffer.Length);
                    progress?.Report((i + 1) / (double)frames * 0.98);
                }
                stdin.Flush();
                stdin.Close();
                proc.WaitForExit();
                if (proc.ExitCode != 0)
                {
                    string tail;
                    lock (errors) tail = string.Join(Environment.NewLine, errors);
                    throw new InvalidOperationException($"ffmpeg failed (exit code {proc.ExitCode}).{Environment.NewLine}{tail}");
                }
                ok = true;
            }
            catch (IOException) when (proc.HasExited)
            {
                // ffmpeg died mid-stream: its own message is the useful one.
                string tail;
                lock (errors) tail = string.Join(Environment.NewLine, errors);
                throw new InvalidOperationException($"ffmpeg stopped early (exit code {proc.ExitCode}).{Environment.NewLine}{tail}");
            }
            finally
            {
                if (!ok)
                {
                    try { if (!proc.HasExited) proc.Kill(true); } catch { }
                    try { proc.WaitForExit(2000); } catch { }
                    TryDelete(o.OutputPath);
                }
            }
        }

        private static readonly Dictionary<string, string> EncoderCache = new();

        /// <summary>
        /// Best H.264 encoder this ffmpeg has: libx264 (GPL builds), else
        /// h264_mf -- Windows' own encoder, present in the LGPL build that
        /// ships with DanaProcessing -- else libopenh264.
        /// </summary>
        private static string[] H264Args(string ffmpeg, int crf)
        {
            string list;
            lock (EncoderCache)
            {
                if (!EncoderCache.TryGetValue(ffmpeg, out list!))
                {
                    try
                    {
                        var psi = new ProcessStartInfo(ffmpeg) { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true };
                        psi.ArgumentList.Add("-hide_banner");
                        psi.ArgumentList.Add("-encoders");
                        using var p = Process.Start(psi)!;
                        p.ErrorDataReceived += (_, _) => { };
                        p.BeginErrorReadLine();
                        list = p.StandardOutput.ReadToEnd();
                        p.WaitForExit(5000);
                    }
                    catch { list = ""; }
                    EncoderCache[ffmpeg] = list;
                }
            }
            if (list.Contains(" libx264 ") || list.Length == 0)
                return new[] { "-c:v", "libx264", "-preset", "medium", "-crf", Math.Clamp(crf, 0, 51).ToString() };
            if (list.Contains(" h264_mf "))
                return new[] { "-c:v", "h264_mf", "-b:v", "16M" };
            if (list.Contains(" libopenh264 "))
                return new[] { "-c:v", "libopenh264", "-b:v", "16M" };
            throw new InvalidOperationException("This ffmpeg has no H.264 encoder (libx264, h264_mf or libopenh264). Export as GIF, or use another ffmpeg build.");
        }

        private static void ExportGif(ReelTimeline timeline, ReelExportOptions o, IProgress<double>? progress, CancellationToken cancel)
        {
            int gw = Math.Clamp(o.GifWidth, 120, timeline.Width);
            gw -= gw % 2;
            float scale = gw / (float)timeline.Width;
            int gh = Math.Max(2, (int)Math.Round(timeline.Height * scale));
            int fps = Math.Clamp(o.GifFps, 5, 50);
            int frames = FrameCount(timeline, fps);

            bool ok = false;
            try
            {
                using (var file = File.Create(o.OutputPath))
                using (var buffered = new BufferedStream(file, 1 << 20))
                using (var gif = new GifEncoder(buffered, gw, gh))
                using (var surface = NewSurface(gw, gh))
                {
                    var buffer = new byte[gw * gh * 4];
                    for (int i = 0; i < frames; i++)
                    {
                        cancel.ThrowIfCancellationRequested();
                        RenderInto(surface, timeline, i / (double)fps, scale);
                        ReadPixels(surface, buffer, gw, gh);
                        // Delays in 1/100 s don't divide evenly at most rates:
                        // spread the rounding so the total length stays exact.
                        int delay = (int)Math.Round(100.0 * (i + 1) / fps) - (int)Math.Round(100.0 * i / fps);
                        gif.AddFrame(buffer, delay);
                        progress?.Report((i + 1) / (double)frames * 0.99);
                    }
                }
                ok = true;
            }
            finally
            {
                if (!ok) TryDelete(o.OutputPath);
            }
        }

        private static void ExportPng(ReelTimeline timeline, ReelExportOptions o, IProgress<double>? progress, CancellationToken cancel)
        {
            Directory.CreateDirectory(o.OutputPath);
            int fps = timeline.Fps;
            int frames = FrameCount(timeline, fps);
            using var surface = NewSurface(timeline.Width, timeline.Height);
            for (int i = 0; i < frames; i++)
            {
                cancel.ThrowIfCancellationRequested();
                RenderInto(surface, timeline, i / (double)fps, 1);
                using var image = surface.Snapshot();
                using var data = image.Encode(SKEncodedImageFormat.Png, 100);
                using var f = File.Create(Path.Combine(o.OutputPath, $"frame_{i + 1:D5}.png"));
                data.SaveTo(f);
                progress?.Report((i + 1) / (double)frames);
            }
        }

        private static void TryDelete(string path)
        {
            try { if (File.Exists(path)) File.Delete(path); } catch { }
        }
    }
}
