using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using DanaProcessing.Ide.Localization;

namespace DanaProcessing.Ide.Blocks
{
    /// <summary>
    /// The names the blocks give things in C# (the turtle, the score, the
    /// variables, the child's own words, the music methods), in the IDE's
    /// language: <c>tortuga.Forward(lado);</c> in Spanish,
    /// <c>turtle.Forward(side);</c> in English. Inside the IDE everything is
    /// kept in Spanish; this only renames whole identifiers, never text in
    /// strings or comments (those are localized where they're written).
    /// </summary>
    public static class CodeWords
    {
        // The child's own words in English are "Make…", like the block
        // ("make a square"): a plain "Square" would sit on top of Sketch.Square().
        private static readonly (string Es, string En)[] Words =
        {
            ("tortuga", "turtle"),
            ("puntos", "score"),
            ("lado", "side"),
            ("angulo", "angle"),
            ("contador", "counter"),
            ("Cuadrado", "MakeSquare"),
            ("Triangulo", "MakeTriangle"),
            ("Escalon", "MakeStep"),
            ("Flor", "MakeFlower"),
            ("Salto", "MakeJump"),
            ("MiTruco", "MyTrick"),
            ("OrdenarElCodigo", "PutTheCodeInOrder"),
        };

        private static readonly Dictionary<string, string> ToEn = Words.ToDictionary(w => w.Es, w => w.En, StringComparer.Ordinal);
        private static readonly Dictionary<string, string> ToEs = Words.ToDictionary(w => w.En, w => w.Es, StringComparer.Ordinal);
        private static readonly Regex MusicEs = new(@"^Musica(\d+)$", RegexOptions.CultureInvariant);
        private static readonly Regex MusicEn = new(@"^Music(\d+)$", RegexOptions.CultureInvariant);

        public static bool English => Loc.Current != AppLanguage.Spanish;

        /// <summary>The code with the blocks' names in the IDE's language.</summary>
        public static string Localize(string code) => English ? ToEnglish(code) : code;

        public static string ToEnglish(string code) => Rename(code, w => ToEn.TryGetValue(w, out var en) ? en : MusicEs.Match(w) is { Success: true } m ? "Music" + m.Groups[1].Value : null);

        /// <summary>English names back to the Spanish ones the IDE keeps (Spanish code passes through unchanged).</summary>
        public static string ToSpanish(string code) => Rename(code, w => ToEs.TryGetValue(w, out var es) ? es : MusicEn.Match(w) is { Success: true } m ? "Musica" + m.Groups[1].Value : null);

        /// <summary>One word (an identifier) in the IDE's language.</summary>
        public static string Word(string spanish) => English && ToEn.TryGetValue(spanish, out var en) ? en : spanish;

        private static string Rename(string code, Func<string, string?> rename)
        {
            var sb = new StringBuilder(code.Length + 16);
            var lines = code.Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                if (i > 0)
                    sb.Append('\n');
                var line = lines[i];
                bool cr = line.EndsWith('\r');
                if (cr)
                    line = line[..^1];
                foreach (var (text, kind) in CSharpTokens.Split(line))
                    sb.Append(kind is CSharpTokens.Kind.Plain or CSharpTokens.Kind.Call && rename(text) is { } other ? other : text);
                if (cr)
                    sb.Append('\r');
            }
            return sb.ToString();
        }
    }
}
