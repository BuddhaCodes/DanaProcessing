using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using DanaProcessing.Ide.Compilation;
using DanaProcessing.Ide.Localization;
using SkiaSharp;

namespace DanaProcessing.Ide.Blocks
{
    /// <summary>
    /// The class, without a server: everything is a file a child or a
    /// teacher can carry on a USB stick or leave in a shared folder.
    /// <list type="bullet">
    /// <item>The child's name and the gallery folder (settings).</item>
    /// <item>The program that passed each challenge, kept so it can be handed in.</item>
    /// <item>A progress file (.danaavance): name, stars and those programs.</item>
    /// <item>The class summary: every progress file in a folder, as a table (and CSV).</item>
    /// <item>The gallery: programs shared into a folder, with a picture of each.</item>
    /// </list>
    /// </summary>
    public static class Classroom
    {
        internal static string AppFolder { get; set; } = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DanaProcessingIde");

        private static string SettingsPath => Path.Combine(AppFolder, "classroom.json");
        private static string SolutionsFolder => Path.Combine(AppFolder, "blocks-solutions");

        public static string DefaultGalleryFolder => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "DanaProcessing", "Galeria");

        internal static readonly JsonSerializerOptions Json = new()
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Converters = { new JsonStringEnumConverter() },
        };

        // =================================================================
        // Settings
        // =================================================================

        public sealed class Settings
        {
            public string StudentName { get; set; } = "";
            public string? GalleryFolder { get; set; }
            [JsonIgnore] public string Gallery => string.IsNullOrWhiteSpace(GalleryFolder) ? DefaultGalleryFolder : GalleryFolder!;
        }

        public static Settings LoadSettings()
        {
            try
            {
                if (File.Exists(SettingsPath))
                    return JsonSerializer.Deserialize<Settings>(File.ReadAllText(SettingsPath), Json) ?? new Settings();
            }
            catch (Exception)
            {
                // a damaged file: start fresh
            }
            return new Settings();
        }

        public static void SaveSettings(Settings settings)
        {
            try
            {
                Directory.CreateDirectory(AppFolder);
                File.WriteAllText(SettingsPath, JsonSerializer.Serialize(settings, Json));
            }
            catch (Exception)
            {
                // not saved: asked again next time
            }
        }

        // =================================================================
        // The answers that passed
        // =================================================================

        /// <summary>Keeps the program that just passed <paramref name="challengeId"/> (the latest pass wins).</summary>
        public static void SaveSolution(string challengeId, BlockProgram program)
        {
            try
            {
                Directory.CreateDirectory(SolutionsFolder);
                File.WriteAllText(Path.Combine(SolutionsFolder, FileSafe(challengeId) + BlockProgram.FileExtension), program.ToJson());
            }
            catch (Exception)
            {
                // a nicety: never interrupts the lesson
            }
        }

        public static Dictionary<string, BlockProgram> LoadSolutions()
        {
            var result = new Dictionary<string, BlockProgram>();
            try
            {
                if (!Directory.Exists(SolutionsFolder))
                    return result;
                foreach (var path in Directory.GetFiles(SolutionsFolder, "*" + BlockProgram.FileExtension))
                {
                    try
                    {
                        var p = BlockProgram.FromJson(File.ReadAllText(path));
                        if (p.ChallengeId is { } id)
                            result[id] = p;
                    }
                    catch (Exception) { }
                }
            }
            catch (Exception) { }
            return result;
        }

        // =================================================================
        // Handing in: the progress file
        // =================================================================

        public sealed class ProgressReport
        {
            public const string Extension = ".danaavance";
            public int Format { get; set; } = 1;
            public string Student { get; set; } = "";
            public DateTime Created { get; set; } = DateTime.Now;
            /// <summary>Challenge id (and "parsons:...") -> best stars.</summary>
            public Dictionary<string, int> Stars { get; set; } = new();
            /// <summary>Challenge id -> the program that passed it (as .danablocks JSON).</summary>
            public Dictionary<string, string> Programs { get; set; } = new();

            public string ToJson() => JsonSerializer.Serialize(this, Json);

            public static ProgressReport FromJson(string json)
            {
                var report = JsonSerializer.Deserialize<ProgressReport>(json, Json) ?? throw new InvalidDataException("empty");
                report.Stars ??= new();
                report.Programs ??= new();
                return report;
            }

            public int TotalStars => Stars.Where(kv => !kv.Key.StartsWith("parsons:", StringComparison.Ordinal)).Sum(kv => kv.Value);

            public BlockProgram? Program(string challengeId) =>
                Programs.TryGetValue(challengeId, out var json) ? BlockProgram.FromJson(json) : null;
        }

        /// <summary>What this computer's child has done, ready to hand in.</summary>
        public static ProgressReport CurrentReport(string student) => new()
        {
            Student = student.Trim(),
            Stars = ChallengeProgress.Load(),
            Programs = LoadSolutions().ToDictionary(kv => kv.Key, kv => kv.Value.ToJson()),
        };

        /// <summary>"Ana-2026-10-30.danaavance": the name a hand-in file gets.</summary>
        public static string ReportFileName(ProgressReport report) =>
            FileSafe(string.IsNullOrWhiteSpace(report.Student) ? "alumno" : report.Student) + "-" +
            report.Created.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) + ProgressReport.Extension;

        // =================================================================
        // The teacher's summary
        // =================================================================

        public sealed record StudentRow(string Student, ProgressReport Report, string File);

        /// <summary>
        /// Every progress file in <paramref name="folder"/> (and below), one row per
        /// child: if a child handed in more than once, the newest file counts.
        /// Unreadable files are listed in <paramref name="broken"/>.
        /// </summary>
        public static List<StudentRow> LoadClass(string folder, out List<string> broken)
        {
            broken = new List<string>();
            var rows = new Dictionary<string, StudentRow>(StringComparer.OrdinalIgnoreCase);
            if (!Directory.Exists(folder))
                return new List<StudentRow>();
            foreach (var path in Directory.GetFiles(folder, "*" + ProgressReport.Extension, SearchOption.AllDirectories))
            {
                try
                {
                    var report = ProgressReport.FromJson(File.ReadAllText(path));
                    var name = string.IsNullOrWhiteSpace(report.Student) ? Path.GetFileNameWithoutExtension(path) : report.Student.Trim();
                    if (!rows.TryGetValue(name, out var existing) || existing.Report.Created < report.Created)
                        rows[name] = new StudentRow(name, report, path);
                }
                catch (Exception)
                {
                    broken.Add(Path.GetFileName(path));
                }
            }
            return rows.Values.OrderBy(r => r.Student, StringComparer.CurrentCultureIgnoreCase).ToList();
        }

        /// <summary>The columns of the summary: built-in challenges, then any other the class has stars in (a teacher's, puzzles).</summary>
        public static List<(string Id, string Title)> Columns(IEnumerable<StudentRow> rows)
        {
            var columns = ChallengeCatalog.All.Select(c => (c.Id, c.Title)).ToList();
            var known = columns.Select(c => c.Id).ToHashSet();
            foreach (var id in rows.SelectMany(r => r.Report.Stars.Keys).Distinct().OrderBy(k => k, StringComparer.Ordinal))
            {
                if (known.Contains(id))
                    continue;
                string title = ChallengeCatalog.Get(id)?.Title
                    ?? (id.StartsWith("parsons:", StringComparison.Ordinal) && ParsonsCatalog.Get(id["parsons:".Length..]) is { } p ? Loc.Tr("Ordenar: ", "Order: ") + p.Title : id);
                columns.Add((id, title));
                known.Add(id);
            }
            return columns;
        }

        /// <summary>The summary as CSV (opens in any spreadsheet): one row per child, stars per challenge, total.</summary>
        public static string ClassCsv(List<StudentRow> rows)
        {
            var columns = Columns(rows);
            var sb = new StringBuilder();
            sb.Append(Csv(Loc.Tr("Alumno", "Student"))).Append(',').Append(Csv(Loc.Tr("Entregado", "Handed in")));
            foreach (var (_, title) in columns)
                sb.Append(',').Append(Csv(title));
            sb.Append(',').Append(Csv(Loc.Tr("Estrellas", "Stars"))).Append('\n');
            foreach (var row in rows)
            {
                sb.Append(Csv(row.Student)).Append(',').Append(row.Report.Created.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture));
                foreach (var (id, _) in columns)
                    sb.Append(',').Append(row.Report.Stars.TryGetValue(id, out var s) ? s.ToString(CultureInfo.InvariantCulture) : "");
                sb.Append(',').Append(row.Report.TotalStars.ToString(CultureInfo.InvariantCulture)).Append('\n');
            }
            return sb.ToString();
        }

        private static string Csv(string text) =>
            text.IndexOfAny(new[] { ',', '"', '\n', '\r' }) >= 0 ? "\"" + text.Replace("\"", "\"\"") + "\"" : text;

        // =================================================================
        // The gallery
        // =================================================================

        public sealed record GalleryItem(string Path, BlockProgram Program, DateTime Shared)
        {
            public string Title => Program.Name;
            public string Author => string.IsNullOrWhiteSpace(Program.Author) ? Loc.Tr("Sin nombre", "No name") : Program.Author!;
        }

        /// <summary>Copies a program into the gallery folder with its author; returns the file.</summary>
        public static string Share(BlockProgram program, string folder, string author)
        {
            Directory.CreateDirectory(folder);
            var copy = program.Clone();
            copy.Author = author.Trim();
            copy.ChallengeId = null;
            var stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
            var path = Path.Combine(folder, $"{FileSafe(copy.Author.Length == 0 ? "alumno" : copy.Author)}-{FileSafe(copy.Name)}-{stamp}{BlockProgram.FileExtension}");
            File.WriteAllText(path, copy.ToJson());
            return path;
        }

        /// <summary>Every program in the gallery folder, newest first.</summary>
        public static List<GalleryItem> Gallery(string folder)
        {
            var list = new List<GalleryItem>();
            if (!Directory.Exists(folder))
                return list;
            foreach (var path in Directory.GetFiles(folder, "*" + BlockProgram.FileExtension))
            {
                try
                {
                    list.Add(new GalleryItem(path, BlockProgram.FromJson(File.ReadAllText(path)), File.GetLastWriteTime(path)));
                }
                catch (Exception) { }
            }
            return list.OrderByDescending(i => i.Shared).ToList();
        }

        /// <summary>
        /// A picture of what the program draws (PNG), for the gallery card:
        /// run headless, turtles finished, a few frames in. Programs with music
        /// aren't run (they'd start the sound); null then, or if it doesn't compile.
        /// </summary>
        public static byte[]? Thumbnail(BlockProgram program, int width = 300, int height = 200)
        {
            if (program.Scripts().Any(s => s.Hat!.Type == BlockCatalog.MusicLoop))
                return null;
            var compiled = SketchCompiler.Compile(BlockCodeGenerator.Generate(program).Source);
            if (!compiled.Success)
                return null;
            var sketch = compiled.Sketch!;
            try
            {
                using var surface = SKSurface.Create(new SKImageInfo(600, 400, SKColorType.Rgba8888, SKAlphaType.Premul));
                sketch.SetCanvas(surface.Canvas, surface);
                sketch.Setup();
                for (int i = 0; i < 3; i++)
                {
                    surface.Canvas.Save();
                    sketch.RenderFrame();
                    surface.Canvas.Restore();
                }
                Turtle[] turtles;
                lock (sketch.Turtles)
                    turtles = sketch.Turtles.ToArray();
                foreach (var t in turtles)
                    t.Finish();
                surface.Canvas.Save();
                sketch.RenderFrame();
                surface.Canvas.Restore();
                surface.Canvas.Flush();
                using var full = surface.Snapshot();
                var crop = ContentBounds(full, width / (float)height);
                using var small = SKSurface.Create(new SKImageInfo(width, height));
                small.Canvas.Clear(SKColors.White);
                using (var paint = new SKPaint { IsAntialias = true })
                    small.Canvas.DrawImage(full, crop, new SKRect(0, 0, width, height), new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear), paint);
                using var image = small.Snapshot();
                using var data = image.Encode(SKEncodedImageFormat.Png, 90);
                return data.ToArray();
            }
            catch (Exception)
            {
                return null;
            }
            finally
            {
                sketch.Dispose();
            }
        }

        /// <summary>
        /// The part of the picture that has something on it (anything unlike
        /// the top-left corner's color), with a margin, widened to the card's
        /// shape -- so a small drawing in the middle fills its card.
        /// </summary>
        internal static SKRect ContentBounds(SKImage image, float aspect)
        {
            using var pixmap = image.PeekPixels();
            var full = new SKRect(0, 0, image.Width, image.Height);
            if (pixmap is null)
                return full;
            var bg = pixmap.GetPixelColor(0, 0);
            int left = image.Width, top = image.Height, right = -1, bottom = -1;
            for (int y = 0; y < image.Height; y += 2)
                for (int x = 0; x < image.Width; x += 2)
                {
                    var c = pixmap.GetPixelColor(x, y);
                    if (Math.Abs(c.Red - bg.Red) + Math.Abs(c.Green - bg.Green) + Math.Abs(c.Blue - bg.Blue) < 40)
                        continue;
                    left = Math.Min(left, x); right = Math.Max(right, x);
                    top = Math.Min(top, y); bottom = Math.Max(bottom, y);
                }
            if (right < 0)
                return full;
            var r = new SKRect(left - 24, top - 24, right + 24, bottom + 24);
            float w = Math.Max(r.Width, 120), h = Math.Max(r.Height, 80);
            if (w / h > aspect) h = w / aspect; else w = h * aspect;
            if (w > full.Width || h > full.Height)
                return full;
            float cx = r.MidX, cy = r.MidY;
            float l = Math.Clamp(cx - w / 2, 0, full.Width - w), t = Math.Clamp(cy - h / 2, 0, full.Height - h);
            return new SKRect(l, t, l + w, t + h);
        }

        internal static string FileSafe(string text)
        {
            var invalid = Path.GetInvalidFileNameChars().Concat("<>:\"/\\|?*").ToHashSet();
            var name = new string(text.Trim().Select(c => invalid.Contains(c) || char.IsWhiteSpace(c) ? '-' : c).ToArray()).Trim('-', '.');
            return name.Length == 0 ? "x" : name.Length > 40 ? name[..40] : name;
        }
    }
}
