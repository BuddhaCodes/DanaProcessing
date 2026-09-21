using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace DanaProcessing.Ide.Compilation.PackageManagement
{
    /// <summary>
    /// One `// nuget: Id[, Version]` line found in a sketch's source. Omitting
    /// the version resolves to the latest stable release on nuget.org every
    /// time the sketch runs — pin a version for reproducible builds.
    /// </summary>
    public sealed record PackageDirective(string Id, string? Version);

    /// <summary>
    /// Reads/writes the `// nuget: Id, Version` directive comments that let a
    /// sketch declare NuGet packages inline, right in its one .cs file —
    /// matching Processing's "the sketch is the whole project" model instead
    /// of introducing a separate project/manifest file. Mirrors the
    /// `#r "nuget: Id, Version"` convention from dotnet-script/Polyglot
    /// Notebooks, just spelled as a plain `//` comment since sketches compile
    /// as ordinary (non-scripting) C#, where `#r` isn't valid syntax.
    /// </summary>
    public static class PackageDirectiveParser
    {
        private static readonly Regex DirectiveLine = new(
            @"^\s*//\s*nuget\s*:\s*(?<id>[A-Za-z0-9_.\-]+)\s*(?:[,\s]\s*(?<version>[A-Za-z0-9.\-+*]+))?\s*$",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        /// <summary>Every `// nuget:` line in <paramref name="source"/>, deduplicated by
        /// package id (case-insensitive, last occurrence wins) so a sketch that lists the
        /// same package twice doesn't try to resolve it twice.</summary>
        public static List<PackageDirective> Parse(string? source)
        {
            var byId = new Dictionary<string, PackageDirective>(System.StringComparer.OrdinalIgnoreCase);
            foreach (var line in SplitLines(source))
            {
                var m = DirectiveLine.Match(line);
                if (!m.Success)
                    continue;

                var id = m.Groups["id"].Value;
                var version = m.Groups["version"].Success ? m.Groups["version"].Value : null;
                byId[id] = new PackageDirective(id, version);
            }
            return byId.Values.ToList();
        }

        /// <summary>
        /// Returns <paramref name="source"/> with every existing `// nuget:` line
        /// stripped out and <paramref name="directives"/> re-inserted as a single
        /// block at the very top (nothing at all if the list is empty) — used by
        /// the "Paquetes NuGet" dialog so adding/removing a package is a plain,
        /// visible text edit the user can see and undo like any other keystroke.
        /// </summary>
        public static string Apply(string? source, IReadOnlyList<PackageDirective> directives)
        {
            var kept = SplitLines(source).Where(l => !DirectiveLine.IsMatch(l)).ToList();
            while (kept.Count > 0 && kept[0].Trim().Length == 0)
                kept.RemoveAt(0);
            var body = string.Join("\n", kept);

            if (directives.Count == 0)
                return body;

            var header = string.Join("\n", directives.Select(d =>
                d.Version is null ? $"// nuget: {d.Id}" : $"// nuget: {d.Id}, {d.Version}"));

            return body.Length == 0 ? header + "\n" : header + "\n\n" + body;
        }

        private static string[] SplitLines(string? source) =>
            (source ?? "").Replace("\r\n", "\n").Split('\n');
    }
}
