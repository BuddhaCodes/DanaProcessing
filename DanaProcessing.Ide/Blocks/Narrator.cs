using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using DanaProcessing.Ide.Localization;

namespace DanaProcessing.Ide.Blocks
{
    /// <summary>
    /// Reads text aloud with the voice the computer already has -- nothing to
    /// download, no internet: Windows' speech (through PowerShell and
    /// System.Speech), macOS' <c>say</c>, Linux's <c>spd-say</c> or
    /// <c>espeak-ng</c>. For children who don't read well yet.
    /// Only one thing is read at a time: reading again stops the last one.
    /// </summary>
    internal static class Narrator
    {
        private static Process? _speaking;
        private static readonly object Gate = new();
        private static Lazy<Engine?> _engine = new(Find);

        private enum Engine { WindowsSpeech, MacSay, SpdSay, Espeak }

        /// <summary>Whether this computer has a voice we know how to use.</summary>
        public static bool IsAvailable => _engine.Value is not null;

        /// <summary>True while something is being read.</summary>
        public static bool IsSpeaking
        {
            get { lock (Gate) return _speaking is { HasExited: false }; }
        }

        /// <summary>Reads <paramref name="text"/> aloud in Spanish or English. Returns false if there's no voice.</summary>
        public static bool Speak(string text, bool spanish)
        {
            if (_engine.Value is not { } engine || string.IsNullOrWhiteSpace(text))
                return false;
            Stop();
            var info = StartInfo(engine, Clean(text), spanish);
            info.UseShellExecute = false;
            info.CreateNoWindow = true;
            try
            {
                var process = Process.Start(info);
                lock (Gate)
                    _speaking = process;
                return process is not null;
            }
            catch (Exception)
            {
                return false; // the voice went away (uninstalled, blocked): just stay quiet
            }
        }

        /// <summary>Stops reading, if it was.</summary>
        public static void Stop()
        {
            Process? p;
            lock (Gate)
            {
                p = _speaking;
                _speaking = null;
            }
            try
            {
                if (p is { HasExited: false })
                    p.Kill(entireProcessTree: true);
            }
            catch (Exception)
            {
                // already gone
            }
            finally
            {
                p?.Dispose();
            }
        }

        /// <summary>
        /// What's read: the text without quote marks, symbols and emoji the
        /// voices would spell out ("comillas", "estrella"...).
        /// </summary>
        internal static string Clean(string text)
        {
            var kept = text.Where(c => char.IsLetterOrDigit(c) || char.IsWhiteSpace(c) || ".,;:¿?¡!()-'°".Contains(c));
            return string.Join(' ', new string(kept.ToArray()).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
                .Replace("°", Loc.Current == AppLanguage.Spanish ? " grados" : " degrees");
        }

        private static ProcessStartInfo StartInfo(Engine engine, string text, bool spanish)
        {
            switch (engine)
            {
                case Engine.WindowsSpeech:
                {
                    // The text goes in through an environment variable, never
                    // into the script itself, so nothing in it can run.
                    const string script =
                        "Add-Type -AssemblyName System.Speech;" +
                        "$s = New-Object System.Speech.Synthesis.SpeechSynthesizer;" +
                        "$v = $s.GetInstalledVoices() | Where-Object { $_.Enabled -and $_.VoiceInfo.Culture.TwoLetterISOLanguageName -eq $env:DANA_SAY_LANG } | Select-Object -First 1;" +
                        "if ($v) { $s.SelectVoice($v.VoiceInfo.Name) };" +
                        "$s.Rate = -1; $s.Speak($env:DANA_SAY_TEXT)";
                    var info = new ProcessStartInfo("powershell.exe");
                    foreach (var a in new[] { "-NoProfile", "-NonInteractive", "-ExecutionPolicy", "Bypass", "-Command", script })
                        info.ArgumentList.Add(a);
                    info.Environment["DANA_SAY_TEXT"] = text;
                    info.Environment["DANA_SAY_LANG"] = spanish ? "es" : "en";
                    return info;
                }
                case Engine.MacSay:
                {
                    var info = new ProcessStartInfo("/usr/bin/say");
                    if (MacVoice(spanish) is { } voice)
                    {
                        info.ArgumentList.Add("-v");
                        info.ArgumentList.Add(voice);
                    }
                    info.ArgumentList.Add("-r");
                    info.ArgumentList.Add("165");
                    info.ArgumentList.Add("--");
                    info.ArgumentList.Add(text);
                    return info;
                }
                case Engine.SpdSay:
                {
                    var info = new ProcessStartInfo("spd-say");
                    foreach (var a in new[] { "-w", "-l", spanish ? "es" : "en", "-r", "-15", "--", text })
                        info.ArgumentList.Add(a);
                    return info;
                }
                default:
                {
                    var info = new ProcessStartInfo(OnPath("espeak-ng") ?? "espeak");
                    foreach (var a in new[] { "-v", spanish ? "es-419" : "en-us", "-s", "150", "--", text })
                        info.ArgumentList.Add(a);
                    return info;
                }
            }
        }

        private static Engine? Find()
        {
            if (OperatingSystem.IsWindows())
                return Engine.WindowsSpeech;
            if (OperatingSystem.IsMacOS())
                return File.Exists("/usr/bin/say") ? Engine.MacSay : null;
            if (OnPath("spd-say") is not null)
                return Engine.SpdSay;
            if (OnPath("espeak-ng") is not null || OnPath("espeak") is not null)
                return Engine.Espeak;
            return null;
        }

        private static string? OnPath(string program)
        {
            var path = Environment.GetEnvironmentVariable("PATH") ?? "";
            foreach (var dir in path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
            {
                var full = Path.Combine(dir, program);
                if (File.Exists(full))
                    return full;
            }
            return null;
        }

        // macOS: a Spanish voice, preferring Latin American ones. "say -v ?"
        // lists them as "Name   es_MX    # sample"; asked once, then remembered.
        private static readonly Dictionary<bool, string?> MacVoices = new();

        private static string? MacVoice(bool spanish)
        {
            lock (MacVoices)
            {
                if (MacVoices.TryGetValue(spanish, out var known))
                    return known;
                string? voice = null;
                try
                {
                    var info = new ProcessStartInfo("/usr/bin/say") { RedirectStandardOutput = true, UseShellExecute = false, CreateNoWindow = true };
                    info.ArgumentList.Add("-v");
                    info.ArgumentList.Add("?");
                    using var p = Process.Start(info)!;
                    var lines = p.StandardOutput.ReadToEnd().Split('\n');
                    p.WaitForExit(3000);
                    voice = PickMacVoice(lines, spanish);
                }
                catch (Exception)
                {
                    // no list: the default voice will do
                }
                MacVoices[spanish] = voice;
                return voice;
            }
        }

        /// <summary>From "say -v ?" output, the best voice for the language (Latin American Spanish first).</summary>
        internal static string? PickMacVoice(IEnumerable<string> lines, bool spanish)
        {
            var voices = new List<(string Name, string Locale)>();
            foreach (var line in lines)
            {
                int hash = line.IndexOf('#');
                var head = (hash >= 0 ? line[..hash] : line).TrimEnd();
                var parts = head.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 2)
                    continue;
                var locale = parts[^1];
                var name = head[..head.LastIndexOf(locale, StringComparison.Ordinal)].Trim();
                voices.Add((name, locale));
            }
            string[] preferred = spanish ? new[] { "es_MX", "es_419", "es_AR", "es_US", "es_CO", "es_ES", "es_" } : new[] { "en_US", "en_GB", "en_" };
            foreach (var p in preferred)
                if (voices.FirstOrDefault(v => v.Locale.StartsWith(p, StringComparison.Ordinal)) is { Name: { Length: > 0 } name })
                    return name;
            return null;
        }

        /// <summary>For tests: look for an engine again.</summary>
        internal static void Reset() => _engine = new Lazy<Engine?>(Find);
    }
}
