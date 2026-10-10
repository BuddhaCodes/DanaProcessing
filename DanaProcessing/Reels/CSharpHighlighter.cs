namespace DanaProcessing.Reels
{
    internal enum TokenKind : byte { Plain, Keyword, Type, Method, String, Number, Comment, Punct }

    internal readonly record struct CodeToken(int Start, int Length, TokenKind Kind);

    /// <summary>
    /// A small, forgiving C# colorizer for the reel's code cards. Not a parser:
    /// it only has to make sketch code look like it does in an editor, one line
    /// at a time, carrying /* */ and verbatim-string state across lines.
    /// </summary>
    internal static class CSharpHighlighter
    {
        private static readonly HashSet<string> Keywords = new(StringComparer.Ordinal)
        {
            "abstract","as","async","await","base","break","case","catch","checked","class","const","continue",
            "default","delegate","do","else","enum","event","explicit","extern","false","finally","fixed","for",
            "foreach","get","goto","if","implicit","in","init","interface","internal","is","lock","namespace","new",
            "null","operator","out","override","params","partial","private","protected","public","readonly","record",
            "ref","required","return","sealed","set","sizeof","stackalloc","static","struct","switch","this","throw",
            "true","try","typeof","unchecked","unsafe","using","var","virtual","volatile","when","where","while",
            "yield","value","and","or","not","with","global",
        };

        private static readonly HashSet<string> BuiltinTypes = new(StringComparer.Ordinal)
        {
            "bool","byte","char","decimal","double","float","int","long","object","sbyte","short","string","uint",
            "ulong","ushort","void","nint","nuint","dynamic",
        };

        internal enum State : byte { Normal, BlockComment, VerbatimString }

        public static List<CodeToken> Tokenize(string line, ref State state)
        {
            var tokens = new List<CodeToken>();
            int i = 0, n = line.Length;

            void Add(int start, int end, TokenKind kind)
            {
                if (end > start)
                    tokens.Add(new CodeToken(start, end - start, kind));
            }

            while (i < n)
            {
                if (state == State.BlockComment)
                {
                    int close = line.IndexOf("*/", i, StringComparison.Ordinal);
                    int end = close < 0 ? n : close + 2;
                    Add(i, end, TokenKind.Comment);
                    i = end;
                    if (close >= 0) state = State.Normal;
                    continue;
                }
                if (state == State.VerbatimString)
                {
                    int j = i;
                    while (j < n)
                    {
                        if (line[j] == '"')
                        {
                            if (j + 1 < n && line[j + 1] == '"') { j += 2; continue; }
                            j++;
                            state = State.Normal;
                            break;
                        }
                        j++;
                    }
                    Add(i, j, TokenKind.String);
                    i = j;
                    continue;
                }

                char c = line[i];

                if (char.IsWhiteSpace(c)) { i++; continue; }

                if (c == '/' && i + 1 < n && line[i + 1] == '/')
                {
                    Add(i, n, TokenKind.Comment);
                    break;
                }
                if (c == '/' && i + 1 < n && line[i + 1] == '*')
                {
                    state = State.BlockComment;
                    int close = line.IndexOf("*/", i + 2, StringComparison.Ordinal);
                    int end = close < 0 ? n : close + 2;
                    Add(i, end, TokenKind.Comment);
                    if (close >= 0) state = State.Normal;
                    i = end;
                    continue;
                }

                // Strings: "..", @"..", $"..", $@"..", @$"..", '.'
                if (c == '"' || c == '\'' || ((c == '@' || c == '$') && i + 1 < n && (line[i + 1] == '"' || ((line[i + 1] == '@' || line[i + 1] == '$') && i + 2 < n && line[i + 2] == '"'))))
                {
                    int start = i;
                    bool verbatim = false;
                    while (i < n && (line[i] == '@' || line[i] == '$'))
                    {
                        if (line[i] == '@') verbatim = true;
                        i++;
                    }
                    char quote = line[i];
                    i++;
                    while (i < n)
                    {
                        if (!verbatim && line[i] == '\\') { i += 2; continue; }
                        if (line[i] == quote)
                        {
                            if (verbatim && i + 1 < n && line[i + 1] == quote) { i += 2; continue; }
                            i++;
                            goto closed;
                        }
                        i++;
                    }
                    if (verbatim && quote == '"') state = State.VerbatimString;
                closed:
                    Add(start, Math.Min(i, n), TokenKind.String);
                    continue;
                }

                if (char.IsDigit(c) || (c == '.' && i + 1 < n && char.IsDigit(line[i + 1])))
                {
                    int start = i;
                    if (c == '0' && i + 1 < n && (line[i + 1] == 'x' || line[i + 1] == 'X'))
                    {
                        i += 2;
                        while (i < n && Uri.IsHexDigit(line[i])) i++;
                    }
                    else
                    {
                        while (i < n && (char.IsDigit(line[i]) || line[i] == '.' || line[i] == '_')) i++;
                        if (i < n && (line[i] == 'e' || line[i] == 'E')) { i++; if (i < n && (line[i] == '+' || line[i] == '-')) i++; while (i < n && char.IsDigit(line[i])) i++; }
                    }
                    while (i < n && "fFdDmMuUlL".IndexOf(line[i]) >= 0) i++;
                    Add(start, i, TokenKind.Number);
                    continue;
                }

                if (char.IsLetter(c) || c == '_' || c == '@')
                {
                    int start = i;
                    i++;
                    while (i < n && (char.IsLetterOrDigit(line[i]) || line[i] == '_')) i++;
                    var word = line.Substring(start, i - start);

                    int k = i;
                    while (k < n && line[k] == ' ') k++;
                    bool call = k < n && line[k] == '(';
                    bool generic = k < n && line[k] == '<' && char.IsUpper(word[0]);

                    TokenKind kind;
                    if (BuiltinTypes.Contains(word)) kind = TokenKind.Type;
                    else if (Keywords.Contains(word)) kind = TokenKind.Keyword;
                    else if (call) kind = TokenKind.Method;
                    else if (char.IsUpper(word[0]) && (generic || LooksLikeType(line, start, i))) kind = TokenKind.Type;
                    else kind = TokenKind.Plain;
                    Add(start, i, kind);
                    continue;
                }

                if ("{}()[];,.:<>=+-*/%!&|^?~".IndexOf(c) >= 0)
                {
                    Add(i, i + 1, TokenKind.Punct);
                    i++;
                    continue;
                }

                Add(i, i + 1, TokenKind.Plain);
                i++;
            }
            return tokens;
        }

        /// <summary>
        /// PascalCase word used as a type: followed by another identifier
        /// ("PVector pos"), "[]" ("float[] xs" is handled by builtin), after
        /// "new", or as a static receiver ("Math.Sin", "Color.Red").
        /// Properties like Width/MouseX stay plain.
        /// </summary>
        private static bool LooksLikeType(string line, int start, int end)
        {
            int k = end;
            while (k < line.Length && line[k] == ' ') k++;
            if (k < line.Length && (char.IsLetter(line[k]) || line[k] == '_') && k > end) return true;   // "PVector pos"
            if (k + 1 < line.Length && line[k] == '[' && line[k + 1] == ']') return true;              // "PVector[] ps"
            if (k < line.Length && line[k] == '?' && k + 1 < line.Length && line[k + 1] == ' ') return true; // "PImage? img"

            int b = start - 1;
            while (b >= 0 && line[b] == ' ') b--;
            if (b >= 2 && line.Substring(0, b + 1).EndsWith("new", StringComparison.Ordinal)) return true;
            if (b >= 0 && line[b] == ':' ) return true; // ": Sketch"
            if (b >= 0 && line[b] == '<') return true;  // "List<Particle>"
            if (end < line.Length && line[end] == '.' && end + 1 < line.Length && char.IsUpper(line[end + 1]))
            {
                // "Math.Sin" / "RendererKind.Renderer3D" -- static receiver.
                return start == 0 || !(line[start - 1] == '.');
            }
            return false;
        }
    }
}
