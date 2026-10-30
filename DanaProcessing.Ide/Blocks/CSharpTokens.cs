using System;
using System.Collections.Generic;

namespace DanaProcessing.Ide.Blocks
{
    /// <summary>
    /// Just enough of a C# tokenizer to color the code shown next to the
    /// blocks. It only ever sees code the block generator wrote (one line at
    /// a time), so it doesn't try to be a real lexer: no verbatim strings,
    /// no multi-line comments.
    /// </summary>
    internal static class CSharpTokens
    {
        public enum Kind { Plain, Keyword, Number, String, Comment, Call, Punctuation }

        private static readonly HashSet<string> Keywords = new(StringComparer.Ordinal)
        {
            "void", "int", "float", "double", "bool", "string", "var", "for", "foreach", "while", "if", "else",
            "return", "new", "true", "false", "null", "class", "public", "private", "static", "override", "in",
            "break", "continue", "using",
        };

        public static IEnumerable<(string Text, Kind Kind)> Split(string line)
        {
            int i = 0;
            while (i < line.Length)
            {
                char c = line[i];
                int start = i;
                if (c == '/' && i + 1 < line.Length && line[i + 1] == '/')
                {
                    yield return (line[i..], Kind.Comment);
                    yield break;
                }
                if (char.IsWhiteSpace(c))
                {
                    while (i < line.Length && char.IsWhiteSpace(line[i])) i++;
                    yield return (line[start..i], Kind.Plain);
                }
                else if (c == '"' || c == '\'')
                {
                    i++;
                    while (i < line.Length && line[i] != c)
                        i += line[i] == '\\' ? 2 : 1;
                    i = Math.Min(i + 1, line.Length);
                    yield return (line[start..i], Kind.String);
                }
                else if (char.IsDigit(c))
                {
                    while (i < line.Length && (char.IsLetterOrDigit(line[i]) || line[i] == '.')) i++;
                    yield return (line[start..i], Kind.Number);
                }
                else if (char.IsLetter(c) || c == '_')
                {
                    while (i < line.Length && (char.IsLetterOrDigit(line[i]) || line[i] == '_')) i++;
                    string word = line[start..i];
                    int next = i;
                    while (next < line.Length && line[next] == ' ') next++;
                    var kind = Keywords.Contains(word) ? Kind.Keyword
                        : next < line.Length && line[next] == '(' ? Kind.Call
                        : Kind.Plain;
                    yield return (word, kind);
                }
                else
                {
                    i++;
                    yield return (line[start..i], Kind.Punctuation);
                }
            }
        }
    }
}
