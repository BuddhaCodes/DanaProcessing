namespace DanaProcessing.Reels
{
    /// <summary>
    /// Long lines on a phone-sized card: rather than shrinking the font
    /// until it can't be read (or cutting the line off), a line wider than
    /// the card wraps onto continuation rows, like an editor's soft wrap.
    /// Breaks go at a space outside string literals, so each row still
    /// highlights correctly; a comment that wraps keeps its "//".
    /// </summary>
    internal static class CodeWrap
    {
        /// <summary>A copy of <paramref name="scene"/> whose lines fit in <paramref name="cols"/> columns; pauses and caption changes follow their lines. The scene itself if nothing needs wrapping.</summary>
        public static ReelScene Wrap(ReelScene scene, int cols)
        {
            if (scene.Lines.All(l => l.Text.Length <= cols))
                return scene;

            var copy = new ReelScene
            {
                Caption = scene.Caption,
                Effect = scene.Effect,
                Transition = scene.Transition,
                Speed = scene.Speed,
                Typos = scene.Typos,
                Hold = scene.Hold,
                Keep = scene.Keep,
                SourceLine = scene.SourceLine,
            };
            var first = new int[scene.Lines.Count + 1]; // original line -> its first row
            for (int i = 0; i < scene.Lines.Count; i++)
            {
                first[i] = copy.Lines.Count;
                var line = scene.Lines[i];
                var rows = WrapLine(line.Text, cols);
                for (int r = 0; r < rows.Count; r++)
                    copy.Lines.Add(new ReelLine { Text = rows[r], Marked = line.Marked, SourceLine = line.SourceLine, Continuation = r > 0 || line.Continuation });
            }
            first[scene.Lines.Count] = copy.Lines.Count;
            foreach (var (l, s) in scene.Pauses)
                copy.Pauses.Add((first[Math.Clamp(l, 0, scene.Lines.Count)], s));
            foreach (var (l, t) in scene.CaptionChanges)
                copy.CaptionChanges.Add((first[Math.Clamp(l, 0, scene.Lines.Count)], t));
            return copy;
        }

        /// <summary>Splits <paramref name="text"/> into rows of at most <paramref name="cols"/> characters (when it can), continuation rows indented one level deeper than the line.</summary>
        public static List<string> WrapLine(string text, int cols)
        {
            var rows = new List<string>();
            cols = Math.Max(12, cols);
            int indent = 0;
            while (indent < text.Length && text[indent] == ' ') indent++;
            string contIndent = new(' ', Math.Min(indent + 4, cols / 3));

            string rest = text;
            bool first = true;
            while (rest.Length > cols)
            {
                int minBreak = (first ? indent : contIndent.Length) + 6;
                var (at, inComment) = BreakPoint(rest, cols, minBreak);
                if (at < 0)
                {
                    // No good place (one huge token): cut it, the row still has to fit.
                    at = cols;
                    inComment = CommentStart(rest) is int c && c >= 0 && c < at;
                    rows.Add(rest.Substring(0, at));
                    rest = contIndent + (inComment ? "// " : "") + rest.Substring(at);
                }
                else
                {
                    rows.Add(rest.Substring(0, at).TrimEnd());
                    rest = contIndent + (inComment ? "// " : "") + rest.Substring(at).TrimStart();
                }
                first = false;
                if (rows.Count > 50) break; // a pathological line; stop rather than spin
            }
            rows.Add(rest);
            return rows;
        }

        /// <summary>Index of the last space at or before <paramref name="cols"/> outside string/char literals (and whether it falls inside a // comment); -1 if none past <paramref name="minBreak"/>.</summary>
        private static (int At, bool InComment) BreakPoint(string s, int cols, int minBreak)
        {
            int best = -1;
            bool bestInComment = false;
            bool inString = false, verbatim = false, inChar = false, inComment = false;
            for (int i = 0; i < s.Length && i <= cols; i++)
            {
                char c = s[i];
                if (inComment)
                {
                    if (c == ' ' && i >= minBreak) { best = i; bestInComment = true; }
                    continue;
                }
                if (inString)
                {
                    if (verbatim) { if (c == '"') { if (i + 1 < s.Length && s[i + 1] == '"') i++; else inString = false; } }
                    else if (c == '\\') i++;
                    else if (c == '"') inString = false;
                    continue;
                }
                if (inChar)
                {
                    if (c == '\\') i++;
                    else if (c == '\'') inChar = false;
                    continue;
                }
                if (c == '/' && i + 1 < s.Length && s[i + 1] == '/') { inComment = true; if (i >= minBreak) { best = i; bestInComment = false; } i++; continue; }
                if (c == '"') { inString = true; verbatim = i > 0 && (s[i - 1] == '@' || (s[i - 1] == '$' && i > 1 && s[i - 2] == '@')); continue; }
                if (c == '\'') { inChar = true; continue; }
                if (c == ' ' && i >= minBreak) { best = i; bestInComment = false; }
            }
            return (best, bestInComment);
        }

        private static int? CommentStart(string s)
        {
            bool inString = false;
            for (int i = 0; i + 1 < s.Length; i++)
            {
                if (s[i] == '"') inString = !inString;
                else if (!inString && s[i] == '/' && s[i + 1] == '/') return i;
            }
            return -1;
        }
    }
}
