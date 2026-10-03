using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;

namespace DanaProcessing.Ide.Agent
{
    /// <summary>One method/property entry from index.html's API reference --
    /// Id is the entry's own stable HTML anchor (e.g. "setup", "pvector-add").</summary>
    public sealed record DocEntry(string Id, string Title, string Description);

    /// <summary>
    /// Parses index.html (the same file the README points people at to read
    /// "locally in a browser") into a flat, searchable list of API entries,
    /// so Agent mode's search_docs tool can look things up by keyword instead
    /// of the model needing the whole ~193KB file stuffed into every request.
    ///
    /// Regex-based, not a real HTML parser -- index.html's own structure is
    /// regular enough for this (confirmed by reading it directly): every
    /// entry is `&lt;h3 class="entry-title" id="..."&gt;TITLE&lt;/h3&gt;` immediately
    /// followed by `&lt;p class="entry-desc"&gt;DESCRIPTION&lt;/p&gt;`, with no
    /// exceptions found. Deliberately doesn't try to also capture the
    /// less-uniformly-structured &lt;div class="note"&gt;/code-sample content that
    /// sits after some (not all) entries -- title+description alone is
    /// enough for "look up what this method does", and chasing perfect
    /// fidelity on the inconsistently-shaped extras isn't worth the
    /// complexity for a first cut.
    ///
    /// index.html is embedded as a resource (see DanaProcessing.Ide.csproj)
    /// rather than read from a relative path on disk -- nothing in this
    /// project referenced the file before Agent mode, so it wasn't
    /// guaranteed to exist next to a published/self-contained exe at all.
    /// </summary>
    public static class ApiDocsIndex
    {
        private static List<DocEntry>? _cache;

        // The title group can't cross a </h3>: otherwise an entry-title with no
        // entry-desc right after it (there was one: "Minimal sketch", followed
        // by a code block) made the lazy match run on into the NEXT entry and
        // swallow it -- that's how Setup() went missing from search_docs.
        private static readonly Regex EntryPattern = new(
            @"<h3\s+class=""entry-title""\s+id=""(?<id>[^""]*)"">(?<title>(?:(?!</h3>).)*?)</h3>\s*<p\s+class=""entry-desc"">(?<desc>.*?)</p>",
            RegexOptions.Singleline | RegexOptions.Compiled);

        private static readonly Regex TagPattern = new("<[^>]+>", RegexOptions.Compiled);

        /// <summary>Case-insensitive substring match against each entry's
        /// title+description, ranked title-matches first (a hit on the
        /// method's own name is almost always what "search for X" means),
        /// returning at most <paramref name="maxResults"/> entries.</summary>
        public static IReadOnlyList<DocEntry> Search(string query, int maxResults = 5)
        {
            var entries = GetEntries();
            if (string.IsNullOrWhiteSpace(query))
                return Array.Empty<DocEntry>();

            return entries
                .Where(e => e.Title.Contains(query, StringComparison.OrdinalIgnoreCase)
                         || e.Description.Contains(query, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(e => e.Title.Contains(query, StringComparison.OrdinalIgnoreCase))
                .Take(maxResults)
                .ToList();
        }

        private static List<DocEntry> GetEntries()
        {
            if (_cache != null)
                return _cache;

            var html = LoadEmbeddedHtml();
            var entries = new List<DocEntry>();
            foreach (Match match in EntryPattern.Matches(html))
            {
                var id = match.Groups["id"].Value;
                var title = CleanText(match.Groups["title"].Value);
                var desc = CleanText(match.Groups["desc"].Value);
                if (title.Length > 0)
                    entries.Add(new DocEntry(id, title, desc));
            }

            _cache = entries;
            return _cache;
        }

        private static string CleanText(string rawHtml)
        {
            var noTags = TagPattern.Replace(rawHtml, "");
            var decoded = noTags
                .Replace("&amp;", "&")
                .Replace("&lt;", "<")
                .Replace("&gt;", ">")
                .Replace("&quot;", "\"")
                .Replace("&#39;", "'");
            return Regex.Replace(decoded, @"\s+", " ").Trim();
        }

        private static string LoadEmbeddedHtml()
        {
            var assembly = Assembly.GetExecutingAssembly();
            using var stream = assembly.GetManifestResourceStream("DanaProcessing.Ide.index.html")
                ?? throw new InvalidOperationException("No se encontró index.html embebido -- revisá el <EmbeddedResource> en DanaProcessing.Ide.csproj.");
            using var reader = new StreamReader(stream, Encoding.UTF8);
            return reader.ReadToEnd();
        }
    }
}
