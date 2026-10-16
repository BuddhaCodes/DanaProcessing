using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace DanaProcessing.Ide.Editor
{
    /// <summary>One saved Run: the code as it was, when, with which seed, and a thumbnail of what it drew.</summary>
    public sealed record CodeVersion(string Id, DateTime TimeUtc, int Seed, string CodePath, string? ThumbnailPath)
    {
        public string ReadCode() => File.ReadAllText(CodePath);
    }

    /// <summary>
    /// The code time machine: every Run keeps a copy of the code (and, a moment
    /// later, a small picture of what it drew) so you can go back to "the
    /// version that looked like that" -- see CodeHistoryWindow. One folder per
    /// sketch (by file path; unsaved sketches share one), a run whose code is
    /// identical to the previous one isn't stored twice, and only the newest
    /// <see cref="MaxVersions"/> are kept.
    ///
    /// Layout: &lt;root&gt;/&lt;sketch key&gt;/&lt;yyyyMMdd-HHmmss-fff&gt;_&lt;seed&gt;.cs (+ .jpg).
    /// </summary>
    public sealed class CodeHistory
    {
        public const int MaxVersions = 200;

        private readonly string _root;

        public CodeHistory(string root) => _root = root;

        /// <summary>The IDE's own store, in the same folder as its other settings.</summary>
        public static CodeHistory Default { get; } = new(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DanaProcessingIde", "history"));

        /// <summary>The folder name for a sketch: a short hash of its file path, or "untitled".</summary>
        public static string KeyFor(string? filePath)
        {
            if (string.IsNullOrEmpty(filePath))
                return "untitled";
            var full = Path.GetFullPath(filePath);
            if (!OperatingSystem.IsLinux())
                full = full.ToLowerInvariant(); // case-insensitive file systems: one sketch, one history
            var hash = SHA1.HashData(Encoding.UTF8.GetBytes(full));
            return Path.GetFileNameWithoutExtension(filePath) + "-" + Convert.ToHexString(hash, 0, 4).ToLowerInvariant();
        }

        private string Folder(string key) => Path.Combine(_root, key);

        /// <summary>
        /// Keeps <paramref name="code"/> as a new version (or returns the newest
        /// one unchanged when the code is identical to it). Never throws: a
        /// history that can't be written just isn't kept.
        /// </summary>
        public CodeVersion? Record(string key, string code, int seed, DateTime? nowUtc = null)
        {
            try
            {
                var latest = List(key).FirstOrDefault();
                if (latest != null && latest.ReadCode() == code)
                    return latest;

                var folder = Folder(key);
                Directory.CreateDirectory(folder);
                var time = nowUtc ?? DateTime.UtcNow;
                string id = time.ToString("yyyyMMdd-HHmmss-fff", CultureInfo.InvariantCulture) + "_" + seed.ToString(CultureInfo.InvariantCulture);
                var path = Path.Combine(folder, id + ".cs");
                File.WriteAllText(path, code);
                Prune(key);
                return new CodeVersion(id, time, seed, path, null);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                DanaLogger.Warn($"Historial de código: no se pudo guardar ({ex.Message}).");
                return null;
            }
        }

        /// <summary>Stores the picture of what a version drew (a JPEG).</summary>
        public CodeVersion AttachThumbnail(CodeVersion version, byte[] jpeg)
        {
            var path = Path.ChangeExtension(version.CodePath, ".jpg");
            try
            {
                File.WriteAllBytes(path, jpeg);
                return version with { ThumbnailPath = path };
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                return version;
            }
        }

        /// <summary>Every kept version of a sketch, newest first.</summary>
        public IReadOnlyList<CodeVersion> List(string key)
        {
            var folder = Folder(key);
            if (!Directory.Exists(folder))
                return Array.Empty<CodeVersion>();

            var versions = new List<CodeVersion>();
            foreach (var path in Directory.EnumerateFiles(folder, "*.cs"))
            {
                string id = Path.GetFileNameWithoutExtension(path);
                var parts = id.Split('_');
                if (!DateTime.TryParseExact(parts[0], "yyyyMMdd-HHmmss-fff", CultureInfo.InvariantCulture,
                        DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var time))
                    continue;
                int seed = parts.Length > 1 && int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var s) ? s : 0;
                var thumb = Path.ChangeExtension(path, ".jpg");
                versions.Add(new CodeVersion(id, time, seed, path, File.Exists(thumb) ? thumb : null));
            }
            return versions.OrderByDescending(v => v.TimeUtc).ThenByDescending(v => v.Id, StringComparer.Ordinal).ToList();
        }

        private void Prune(string key)
        {
            foreach (var old in List(key).Skip(MaxVersions))
            {
                File.Delete(old.CodePath);
                if (old.ThumbnailPath != null)
                    File.Delete(old.ThumbnailPath);
            }
        }

        /// <summary>
        /// Lines added and removed going from <paramref name="from"/> to
        /// <paramref name="to"/> (a line-level LCS diff; whitespace at the ends of
        /// lines is ignored) -- the "+12 −3" on each card.
        /// </summary>
        public static (int Added, int Removed) LineDiff(string from, string to)
        {
            var a = from.Replace("\r\n", "\n").Split('\n').Select(l => l.Trim()).ToArray();
            var b = to.Replace("\r\n", "\n").Split('\n').Select(l => l.Trim()).ToArray();

            // Trim the common head and tail first: most edits touch a few lines.
            int start = 0;
            while (start < a.Length && start < b.Length && a[start] == b[start])
                start++;
            int endA = a.Length, endB = b.Length;
            while (endA > start && endB > start && a[endA - 1] == b[endB - 1])
            {
                endA--;
                endB--;
            }
            int n = endA - start, m = endB - start;
            if (n == 0 || m == 0)
                return (m, n);
            if ((long)n * m > 4_000_000)
                return (m, n); // huge rewrite: don't spend the time

            var lcs = new int[n + 1, m + 1];
            for (int i = n - 1; i >= 0; i--)
                for (int j = m - 1; j >= 0; j--)
                    lcs[i, j] = a[start + i] == b[start + j] ? lcs[i + 1, j + 1] + 1 : Math.Max(lcs[i + 1, j], lcs[i, j + 1]);
            int common = lcs[0, 0];
            return (m - common, n - common);
        }
    }
}
