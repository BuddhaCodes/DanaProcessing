using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace DanaProcessing.Reels
{
    /// <summary>Output frame shape. Vertical (1080x1920) is the Reels / Shorts / TikTok format.</summary>
    public enum ReelFormat { Vertical, Square, Landscape }

    /// <summary>How a scene's code appears.</summary>
    public enum CodeEffect
    {
        /// <summary>Typed character by character, with a caret -- the "someone is writing it" look.</summary>
        Type,
        /// <summary>Line by line, each sliding up into place.</summary>
        Lines,
        /// <summary>The whole block fades in.</summary>
        Fade,
        /// <summary>Already there.</summary>
        Instant,
    }

    /// <summary>How one part of the reel hands over to the next.</summary>
    public enum ReelTransition { Fade, Slide, Zoom, Wipe, None }

    public enum ReelThemeKind { Clay, Dark, Light }

    /// <summary>What the simulated mouse does in the result part.</summary>
    public enum ReelMouseMode
    {
        /// <summary>If the sketch reacts to the mouse, invent a natural-looking path (plus clicks/drags if it handles them). Overridden by any explicit move/drag/click marker.</summary>
        Auto,
        /// <summary>No simulated mouse, no cursor drawn.</summary>
        None,
    }

    /// <summary>One line of code as it will be shown (markers removed, indentation normalized).</summary>
    public sealed class ReelLine
    {
        public required string Text { get; init; }
        /// <summary>Line ended with <c>// @mark</c>: highlighted once the scene's code is shown.</summary>
        public bool Marked { get; init; }
        /// <summary>1-based line in the original source, for messages.</summary>
        public int SourceLine { get; init; }
    }

    /// <summary>A card of code: from one <c>// @reel scene</c> marker to the next.</summary>
    public sealed class ReelScene
    {
        public string? Caption { get; set; }
        public CodeEffect Effect { get; set; } = CodeEffect.Type;
        /// <summary>Null = the reel's default for this position.</summary>
        public ReelTransition? Transition { get; set; }
        /// <summary>Characters per second for Type. Null = the reel's Speed.</summary>
        public double? Speed { get; set; }
        /// <summary>Seconds the finished card stays on screen.</summary>
        public double Hold { get; set; } = 1.4;
        /// <summary>Continue the previous card (its code stays, this scene's code is added below) instead of starting a new one.</summary>
        public bool Keep { get; set; }
        public List<ReelLine> Lines { get; } = new();
        /// <summary>Pause N seconds before typing line index L (index into Lines).</summary>
        public List<(int Line, double Seconds)> Pauses { get; } = new();
        /// <summary>Caption changes to Text when line index L starts appearing.</summary>
        public List<(int Line, string Text)> CaptionChanges { get; } = new();
        public int SourceLine { get; init; }
    }

    public enum ReelInputKind { Move, Drag, Click, Key }

    /// <summary>A scripted interaction for the result part. Times are seconds since the result started; points are in sketch pixels.</summary>
    public sealed class ReelInputAction
    {
        public ReelInputKind Kind { get; init; }
        public List<(float X, float Y)> Points { get; init; } = new();
        public double At { get; init; }
        public double Duration { get; init; }
        public string? Key { get; init; }
        public int SourceLine { get; init; }
    }

    /// <summary>
    /// Everything the <c>// @reel</c> markers in a sketch say about the video.
    /// Pure data: parsing never touches SkiaSharp or the sketch itself, so the
    /// IDE can re-parse on every keystroke if it wants to.
    ///
    /// <code>
    /// using DanaProcessing.Reels;          // opt in: the IDE offers "Create reel"
    ///
    /// // @reel title: Particles that follow you
    /// // @reel format: vertical
    /// // @reel result: 8
    ///
    /// // @reel scene "The canvas" effect=type
    /// public override void Setup()
    /// {
    ///     Size(600, 600);                 // @mark
    /// }
    ///
    /// // @reel scene "Every frame" transition=slide
    /// // @reel pause 0.6
    /// public override void Draw() { ... }
    ///
    /// // @reel drag 100,100 -> 500,400 at=2 dur=1.5
    /// </code>
    /// </summary>
    public sealed class ReelScript
    {
        public string? Title { get; set; }
        public string? Subtitle { get; set; }
        /// <summary>Closing card text. Null = default ("Made with DanaProcessing"); empty = no closing card.</summary>
        public string? Outro { get; set; }
        public ReelFormat Format { get; set; } = ReelFormat.Vertical;
        public ReelThemeKind Theme { get; set; } = ReelThemeKind.Clay;
        /// <summary>Default typing speed, characters per second.</summary>
        public double Speed { get; set; } = 55;
        /// <summary>True when the speed was set explicitly (otherwise very long scenes speed up to stay reel-sized).</summary>
        public bool SpeedExplicit { get; set; }
        /// <summary>Seconds the running sketch is shown at the end. 0 = no result part.</summary>
        public double ResultSeconds { get; set; } = 6;
        public ReelTransition ResultTransition { get; set; } = ReelTransition.Zoom;
        public string? ResultCaption { get; set; }
        public int Fps { get; set; } = 30;
        /// <summary>"en" or "es": language of the few words the reel draws itself ("Result", the outro).</summary>
        public string Lang { get; set; } = "en";
        public string FileName { get; set; } = "Sketch.cs";
        public bool ShowProgress { get; set; } = true;
        public bool ShowLineNumbers { get; set; } = true;
        public ReelMouseMode Mouse { get; set; } = ReelMouseMode.Auto;

        public List<ReelScene> Scenes { get; } = new();
        public List<ReelInputAction> Input { get; } = new();
        public List<string> Warnings { get; } = new();

        /// <summary>The source has <c>using DanaProcessing.Reels;</c>.</summary>
        public bool ImportsModule { get; private set; }
        /// <summary>The source has at least one <c>// @reel</c> marker.</summary>
        public bool HasMarkers { get; private set; }
        /// <summary>The source reads the mouse (MouseX, IsMousePressed...) -- used by Mouse=Auto together with the sketch's overrides.</summary>
        public bool SourceUsesMouse { get; private set; }

        public int Width => Format switch { ReelFormat.Square => 1080, ReelFormat.Landscape => 1920, _ => 1080 };
        public int Height => Format switch { ReelFormat.Square => 1080, ReelFormat.Landscape => 1080, _ => 1920 };

        private static readonly Regex UsingReels = new(@"^\s*using\s+DanaProcessing\.Reels\s*;", RegexOptions.Compiled);
        private static readonly Regex MarkerLine = new(@"^\s*//\s*@reel\b(?<rest>.*)$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex TrailingMark = new(@"\s*//\s*@mark\b.*$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex Token = new("(?<k>[A-Za-z_]+)=\"(?<kv>[^\"]*)\"|\"(?<q>[^\"]*)\"|(?<w>\\S+)", RegexOptions.Compiled);
        private static readonly Regex MouseUse = new(@"\b(MouseX|MouseY|PMouseX|PMouseY|IsMousePressed|MouseButton)\b", RegexOptions.Compiled);

        public static ReelScript Parse(string source)
        {
            var script = new ReelScript();
            var lines = source.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');

            ReelScene? current = null;
            var pendingPauses = new List<double>();
            string? pendingCaption = null;
            bool hidden = false;
            int hideStartLine = 0;

            ReelScene EnsureScene(int sourceLine)
            {
                if (current == null)
                {
                    current = new ReelScene { SourceLine = sourceLine };
                    script.Scenes.Add(current);
                }
                return current;
            }

            for (int i = 0; i < lines.Length; i++)
            {
                int lineNo = i + 1;
                var raw = lines[i];

                if (UsingReels.IsMatch(raw))
                {
                    script.ImportsModule = true;
                    continue; // meta: never shown in the video
                }

                var m = MarkerLine.Match(raw);
                if (m.Success)
                {
                    script.HasMarkers = true;
                    var rest = m.Groups["rest"].Value.Trim();
                    var (command, text, args, opts) = SplitCommand(rest);

                    switch (command)
                    {
                        case "title": script.Title = text; break;
                        case "subtitle": script.Subtitle = text; break;
                        case "outro":
                            script.Outro = IsOff(text) ? "" : text;
                            break;
                        case "file": case "filename": script.FileName = string.IsNullOrWhiteSpace(text) ? "Sketch.cs" : text!; break;
                        case "format": script.Format = ParseFormat(text, lineNo, script); break;
                        case "theme": script.Theme = ParseEnum(text, ReelThemeKind.Clay, lineNo, script, "theme"); break;
                        case "lang": case "language":
                            script.Lang = (text ?? "en").Trim().ToLowerInvariant().StartsWith("es") ? "es" : "en";
                            break;
                        case "speed":
                            script.Speed = ParseSpeed(text, script.Speed, lineNo, script);
                            script.SpeedExplicit = true;
                            break;
                        case "fps":
                            script.Fps = Math.Clamp((int)ParseNumber(text, 30, lineNo, script), 12, 60);
                            break;
                        case "progress": script.ShowProgress = !IsOff(text); break;
                        case "linenumbers": case "numbers": script.ShowLineNumbers = !IsOff(text); break;
                        case "mouse":
                            script.Mouse = IsOff(text) ? ReelMouseMode.None : ReelMouseMode.Auto;
                            break;
                        case "result":
                        {
                            var first = args.Count > 0 ? args[0] : text;
                            if (IsOff(first))
                                script.ResultSeconds = 0;
                            else if (!string.IsNullOrWhiteSpace(first))
                                script.ResultSeconds = Math.Clamp(ParseNumber(first.TrimEnd('s'), 6, lineNo, script), 0, 120);
                            if (opts.TryGetValue("transition", out var rt))
                                script.ResultTransition = ParseEnum(rt, ReelTransition.Zoom, lineNo, script, "transition");
                            if (opts.TryGetValue("caption", out var rc))
                                script.ResultCaption = rc;
                            else if (args.Count == 0 && text != null && !double.TryParse(text.TrimEnd('s'), NumberStyles.Float, CultureInfo.InvariantCulture, out _))
                                script.ResultCaption = text;
                            break;
                        }

                        case "scene":
                        {
                            FlushPending(current, ref pendingCaption, pendingPauses);
                            current = new ReelScene { SourceLine = lineNo, Caption = text };
                            foreach (var (k, v) in opts)
                            {
                                switch (k)
                                {
                                    case "effect": current.Effect = ParseEnum(v, CodeEffect.Type, lineNo, script, "effect"); break;
                                    case "transition": current.Transition = ParseEnum(v, ReelTransition.Slide, lineNo, script, "transition"); break;
                                    case "speed": current.Speed = ParseSpeed(v, script.Speed, lineNo, script); break;
                                    case "hold": current.Hold = Math.Clamp(ParseNumber(v.TrimEnd('s'), 1.4, lineNo, script), 0, 30); break;
                                    case "caption": current.Caption = v; break;
                                    default: script.Warnings.Add(W(lineNo, $"unknown scene option '{k}'")); break;
                                }
                            }
                            foreach (var a in args)
                            {
                                if (a.Equals("keep", StringComparison.OrdinalIgnoreCase) || a.Equals("continue", StringComparison.OrdinalIgnoreCase))
                                    current.Keep = true;
                                else if (Enum.TryParse<CodeEffect>(a, true, out var eff))
                                    current.Effect = eff;
                                else
                                    script.Warnings.Add(W(lineNo, $"unknown scene word '{a}'"));
                            }
                            script.Scenes.Add(current);
                            break;
                        }

                        case "caption":
                            pendingCaption = text ?? "";
                            break;
                        case "pause":
                        case "wait":
                            pendingPauses.Add(Math.Clamp(ParseNumber((args.Count > 0 ? args[0] : text ?? "0.5").TrimEnd('s'), 0.5, lineNo, script), 0, 20));
                            break;

                        case "hide":
                            if (hidden) script.Warnings.Add(W(lineNo, "'hide' while already hidden"));
                            hidden = true;
                            hideStartLine = lineNo;
                            break;
                        case "show":
                        case "endhide":
                        case "unhide":
                            hidden = false;
                            break;

                        case "move":
                        case "drag":
                        case "click":
                        case "key":
                            ParseInput(command, args, opts, text, lineNo, script);
                            break;

                        case "":
                            script.Warnings.Add(W(lineNo, "empty @reel marker"));
                            break;
                        default:
                            script.Warnings.Add(W(lineNo, $"unknown command '{command}'"));
                            break;
                    }
                    continue;
                }

                if (hidden)
                    continue;

                // Ordinary code line.
                bool marked = false;
                var code = raw;
                var tm = TrailingMark.Match(code);
                if (tm.Success)
                {
                    marked = true;
                    code = code.Substring(0, tm.Index);
                }
                code = code.Replace("\t", "    ").TrimEnd();

                if (MouseUse.IsMatch(code))
                    script.SourceUsesMouse = true;

                // Blank lines before a scene's first real line are dropped
                // (they are just spacing between markers in the source).
                if (code.Length == 0 && (current == null || current.Lines.Count == 0))
                    continue;

                var scene = EnsureScene(lineNo);
                foreach (var p in pendingPauses)
                    scene.Pauses.Add((scene.Lines.Count, p));
                pendingPauses.Clear();
                if (pendingCaption != null)
                {
                    scene.CaptionChanges.Add((scene.Lines.Count, pendingCaption));
                    pendingCaption = null;
                }
                scene.Lines.Add(new ReelLine { Text = code, Marked = marked, SourceLine = lineNo });
            }

            if (hidden)
                script.Warnings.Add(W(hideStartLine, "'hide' without a matching 'show' -- everything after it is hidden"));

            // Tidy every scene: trailing blank lines go, common indentation goes
            // (a method body shown on its own shouldn't sit 8 spaces to the right).
            foreach (var s in script.Scenes)
            {
                while (s.Lines.Count > 0 && s.Lines[^1].Text.Length == 0)
                    s.Lines.RemoveAt(s.Lines.Count - 1);
            }
            // Scenes that "keep" share one card, so they share one indentation.
            for (int a = 0; a < script.Scenes.Count;)
            {
                int b = a + 1;
                while (b < script.Scenes.Count && script.Scenes[b].Keep) b++;
                Dedent(script.Scenes.GetRange(a, b - a));
                a = b;
            }

            // Scenes left with no code (e.g. two markers in a row) are dropped,
            // but a caption-only scene is a legitimate "talking" card -- keep it
            // only when it has a caption.
            script.Scenes.RemoveAll(s => s.Lines.Count == 0 && string.IsNullOrWhiteSpace(s.Caption));

            if (script.Scenes.Count > 0 && script.Scenes[0].Keep)
                script.Scenes[0].Keep = false;

            return script;
        }

        private static void FlushPending(ReelScene? scene, ref string? pendingCaption, List<double> pendingPauses)
        {
            // A caption/pause right before a new scene marker has no line to
            // attach to in the old scene: it simply belongs to nothing.
            pendingCaption = null;
            pendingPauses.Clear();
        }

        private static void Dedent(List<ReelScene> chain)
        {
            int min = int.MaxValue;
            foreach (var s in chain)
                foreach (var l in s.Lines)
                {
                    if (l.Text.Length == 0) continue;
                    int n = 0;
                    while (n < l.Text.Length && l.Text[n] == ' ') n++;
                    min = Math.Min(min, n);
                }
            if (min == 0 || min == int.MaxValue)
                return;
            foreach (var s in chain)
                for (int i = 0; i < s.Lines.Count; i++)
                {
                    var l = s.Lines[i];
                    if (l.Text.Length == 0) continue;
                    s.Lines[i] = new ReelLine { Text = l.Text.Substring(min), Marked = l.Marked, SourceLine = l.SourceLine };
                }
        }

        private static (string command, string? text, List<string> args, Dictionary<string, string> opts) SplitCommand(string rest)
        {
            var args = new List<string>();
            var opts = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (rest.Length == 0)
                return ("", null, args, opts);

            int cut = 0;
            while (cut < rest.Length && (char.IsLetter(rest[cut]) || rest[cut] == '-' || rest[cut] == '_'))
                cut++;
            var command = rest.Substring(0, cut).ToLowerInvariant();
            var tail = rest.Substring(cut).Trim();

            // "title: Some words, with: punctuation" -- everything after ':' is text.
            if (tail.StartsWith(':'))
            {
                if (command is "title" or "subtitle" or "outro" or "caption" or "file" or "filename")
                    return (command, tail.Substring(1).Trim(), args, opts);
                tail = tail.Substring(1).Trim(); // "result: 8 caption=..." reads like "result 8 caption=..."
            }

            string? text = null;
            foreach (Match t in Token.Matches(tail))
            {
                if (t.Groups["k"].Success)
                {
                    opts[t.Groups["k"].Value] = t.Groups["kv"].Value;
                    continue;
                }
                if (t.Groups["q"].Success)
                {
                    text = text == null ? t.Groups["q"].Value : text + " " + t.Groups["q"].Value;
                    continue;
                }
                var w = t.Groups["w"].Value;
                int eq = w.IndexOf('=');
                if (eq > 0)
                    opts[w.Substring(0, eq)] = w.Substring(eq + 1);
                else
                    args.Add(w);
            }

            // Bare words with no quotes and no options: treat them as the text
            // ("// @reel title My sketch" works like "title: My sketch").
            if (text == null && opts.Count == 0 && args.Count > 0 && command is "title" or "subtitle" or "outro" or "caption" or "file" or "filename")
            {
                text = string.Join(" ", args);
                args.Clear();
            }
            else if (text == null && args.Count == 1 && command is "format" or "theme" or "lang" or "language" or "speed" or "fps" or "mouse" or "progress" or "linenumbers" or "numbers")
            {
                text = args[0];
                args.Clear();
            }

            return (command, text, args, opts);
        }

        private static void ParseInput(string command, List<string> args, Dictionary<string, string> opts, string? text, int lineNo, ReelScript script)
        {
            double at = opts.TryGetValue("at", out var atS) ? ParseNumber(atS.TrimEnd('s'), 0, lineNo, script) : double.NaN;
            double dur = opts.TryGetValue("dur", out var dS) || opts.TryGetValue("duration", out dS) || opts.TryGetValue("over", out dS)
                ? ParseNumber(dS.TrimEnd('s'), 1, lineNo, script)
                : double.NaN;

            // Also accept the readable "at 2s" / "over 1.5s" forms as plain words.
            var words = new List<string>();
            for (int i = 0; i < args.Count; i++)
            {
                var a = args[i];
                if ((a.Equals("at", StringComparison.OrdinalIgnoreCase) || a.Equals("over", StringComparison.OrdinalIgnoreCase) || a.Equals("for", StringComparison.OrdinalIgnoreCase)) && i + 1 < args.Count)
                {
                    var v = ParseNumber(args[i + 1].TrimEnd('s'), 0, lineNo, script);
                    if (a.Equals("at", StringComparison.OrdinalIgnoreCase)) at = v; else dur = v;
                    i++;
                    continue;
                }
                if (a is "->" or "→" or "to")
                    continue;
                words.Add(a);
            }

            if (command == "key")
            {
                var key = text ?? (words.Count > 0 ? words[0] : null);
                if (string.IsNullOrEmpty(key))
                {
                    script.Warnings.Add(W(lineNo, "key needs a key name, e.g. // @reel key space at=3"));
                    return;
                }
                script.Input.Add(new ReelInputAction { Kind = ReelInputKind.Key, Key = key, At = double.IsNaN(at) ? 0 : at, SourceLine = lineNo });
                return;
            }

            var points = new List<(float, float)>();
            bool badPoint = false;
            foreach (var w in words)
            {
                foreach (var part in w.Split(new[] { "->", "→" }, StringSplitOptions.RemoveEmptyEntries))
                {
                    var xy = part.Trim('(', ')').Split(',');
                    if (xy.Length == 2
                        && float.TryParse(xy[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var x)
                        && float.TryParse(xy[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var y))
                        points.Add((x, y));
                    else
                    {
                        script.Warnings.Add(W(lineNo, $"'{part}' is not a point -- write points as x,y (sketch pixels)"));
                        badPoint = true;
                    }
                }
            }

            var kind = command switch { "drag" => ReelInputKind.Drag, "click" => ReelInputKind.Click, _ => ReelInputKind.Move };
            int needed = kind == ReelInputKind.Click ? 1 : 2;
            if (badPoint)
                return;
            if (points.Count < needed)
            {
                script.Warnings.Add(W(lineNo, kind == ReelInputKind.Click
                    ? "click needs a point, e.g. // @reel click 300,200 at=2"
                    : $"{command} needs at least two points, e.g. // @reel {command} 100,100 -> 400,300 at=1 dur=2"));
                return;
            }

            script.Input.Add(new ReelInputAction
            {
                Kind = kind,
                Points = points,
                At = double.IsNaN(at) ? 0 : Math.Max(0, at),
                Duration = kind == ReelInputKind.Click ? 0 : (double.IsNaN(dur) ? 1.5 : Math.Max(0.05, dur)),
                SourceLine = lineNo,
            });
        }

        private static ReelFormat ParseFormat(string? v, int line, ReelScript s)
        {
            switch ((v ?? "").Trim().ToLowerInvariant())
            {
                case "vertical": case "9:16": case "reel": case "reels": case "story": case "short": case "shorts": case "tiktok": case "portrait":
                    return ReelFormat.Vertical;
                case "square": case "1:1": case "post":
                    return ReelFormat.Square;
                case "landscape": case "16:9": case "wide": case "youtube": case "horizontal":
                    return ReelFormat.Landscape;
                default:
                    s.Warnings.Add(W(line, $"unknown format '{v}' (vertical, square, landscape)"));
                    return ReelFormat.Vertical;
            }
        }

        private static double ParseSpeed(string? v, double fallback, int line, ReelScript s)
        {
            switch ((v ?? "").Trim().ToLowerInvariant())
            {
                case "slow": return 28;
                case "normal": return 45;
                case "fast": return 80;
                case "turbo": case "faster": return 140;
            }
            return Math.Clamp(ParseNumber(v, fallback, line, s), 2, 2000);
        }

        private static T ParseEnum<T>(string? v, T fallback, int line, ReelScript s, string what) where T : struct, Enum
        {
            if (v != null && Enum.TryParse<T>(v.Trim(), true, out var r) && !int.TryParse(v, out _))
                return r;
            s.Warnings.Add(W(line, $"unknown {what} '{v}' ({string.Join(", ", Enum.GetNames<T>()).ToLowerInvariant()})"));
            return fallback;
        }

        private static double ParseNumber(string? v, double fallback, int line, ReelScript s)
        {
            if (v != null && double.TryParse(v.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var d) && double.IsFinite(d))
                return d;
            s.Warnings.Add(W(line, $"'{v}' is not a number"));
            return fallback;
        }

        private static bool IsOff(string? v) => v != null && v.Trim().ToLowerInvariant() is "off" or "none" or "no" or "false" or "0" or "hide";

        private static string W(int line, string message) => $"line {line}: {message}";

        /// <summary>A short human summary, e.g. for the IDE: "3 scenes · 41 lines · result 8 s".</summary>
        public string Describe()
        {
            var sb = new StringBuilder();
            sb.Append(Scenes.Count).Append(Scenes.Count == 1 ? " scene" : " scenes");
            sb.Append(" · ").Append(Scenes.Sum(s => s.Lines.Count)).Append(" lines");
            if (ResultSeconds > 0) sb.Append(" · result ").Append(ResultSeconds.ToString("0.#", CultureInfo.InvariantCulture)).Append(" s");
            return sb.ToString();
        }
    }
}
