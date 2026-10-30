using DanaProcessing.Ide.Blocks;
using Xunit;

namespace DanaProcessing.Tests.Ide;

[Collection("ChallengeFiles")]
public class ClassroomTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "dana-aula-" + Guid.NewGuid().ToString("N"));
    private readonly string _oldApp = Classroom.AppFolder;
    private readonly string _oldProgress = ChallengeProgress.FilePath;

    public ClassroomTests()
    {
        Classroom.AppFolder = Path.Combine(_root, "app");
        ChallengeProgress.FilePath = Path.Combine(_root, "app", "blocks-progress.json");
    }

    public void Dispose()
    {
        Classroom.AppFolder = _oldApp;
        ChallengeProgress.FilePath = _oldProgress;
        try { Directory.Delete(_root, true); } catch { }
    }

    private static BlockProgram Square(string? challenge = "repeat")
    {
        var p = new BlockProgram { Name = "Cuadrado", ChallengeId = challenge };
        var s = p.AddStack(40, 40, BlockCatalog.Start);
        var rep = p.NewBlock(BlockCatalog.Repeat);
        rep.Body!.Add(p.NewBlock(BlockCatalog.Forward));
        rep.Body!.Add(p.NewBlock(BlockCatalog.Right));
        s.Blocks.Add(rep);
        return p;
    }

    [Fact]
    public void Settings_remember_the_name_and_the_gallery()
    {
        Assert.Equal("", Classroom.LoadSettings().StudentName);
        Assert.Equal(Classroom.DefaultGalleryFolder, Classroom.LoadSettings().Gallery);
        Classroom.SaveSettings(new Classroom.Settings { StudentName = "Ana", GalleryFolder = "/compartida/galeria" });
        var back = Classroom.LoadSettings();
        Assert.Equal("Ana", back.StudentName);
        Assert.Equal("/compartida/galeria", back.Gallery);
    }

    [Fact]
    public void A_hand_in_carries_the_stars_and_the_programs_that_passed()
    {
        ChallengeProgress.Record("repeat", 3);
        ChallengeProgress.Record("parsons:square", 3);
        Classroom.SaveSolution("repeat", Square());

        var report = Classroom.CurrentReport("  Ana  ");
        Assert.Equal("Ana", report.Student);
        Assert.Equal(3, report.Stars["repeat"]);
        Assert.Equal(3, report.TotalStars);                         // puzzles aren't challenge stars
        var back = Classroom.ProgressReport.FromJson(report.ToJson());
        var program = back.Program("repeat")!;
        Assert.Equal(BlockCatalog.Repeat, program.Stacks[0].Blocks[1].Type);
        Assert.Matches(@"^Ana-\d{4}-\d{2}-\d{2}\.danaavance$", Classroom.ReportFileName(back));
    }

    [Fact]
    public void The_class_summary_keeps_each_child_s_newest_hand_in_and_exports_csv()
    {
        var folder = Path.Combine(_root, "entregas");
        Directory.CreateDirectory(Path.Combine(folder, "pendrive"));
        void HandIn(string file, string name, DateTime when, params (string Id, int Stars)[] stars) =>
            File.WriteAllText(Path.Combine(folder, file), new Classroom.ProgressReport
            {
                Student = name, Created = when, Stars = stars.ToDictionary(s => s.Id, s => s.Stars),
            }.ToJson());

        HandIn("ana-1.danaavance", "Ana", new DateTime(2026, 10, 1), ("first-step", 3));
        HandIn("ana-2.danaavance", "ana", new DateTime(2026, 10, 8), ("first-step", 3), ("corner", 2));
        HandIn("pendrive/beto.danaavance", "Beto, el \"rápido\"", new DateTime(2026, 10, 7), ("parsons:corner", 3), ("file:abc", 1));
        File.WriteAllText(Path.Combine(folder, "roto.danaavance"), "no es json");

        var rows = Classroom.LoadClass(folder, out var broken);
        Assert.Equal(new[] { "ana", "Beto, el \"rápido\"" }, rows.Select(r => r.Student));
        Assert.Equal(5, rows[0].Report.TotalStars);                 // the newest of Ana's two
        Assert.Equal("roto.danaavance", Assert.Single(broken));

        var columns = Classroom.Columns(rows);
        Assert.Equal(ChallengeCatalog.All.Count + 2, columns.Count);
        Assert.Contains(columns, c => c.Id == "parsons:corner" && c.Title.Contains("esquina", StringComparison.OrdinalIgnoreCase));

        var csv = Classroom.ClassCsv(rows).Split('\n');
        Assert.StartsWith("Alumno,Entregado,", csv[0]);
        Assert.StartsWith("ana,2026-10-08 00:00,3,2,", csv[1]);
        Assert.StartsWith("\"Beto, el \"\"rápido\"\"\",", csv[2]);
        Assert.EndsWith(",1", csv[2]);
    }

    [Fact]
    public void Shared_programs_show_up_in_the_gallery_with_their_author_and_a_picture()
    {
        var gallery = Path.Combine(_root, "galeria");
        var path = Classroom.Share(Square(), gallery, "Ana");
        Assert.DoesNotContain(":", Path.GetFileName(path));
        var item = Assert.Single(Classroom.Gallery(gallery));
        Assert.Equal("Ana", item.Author);
        Assert.Null(item.Program.ChallengeId);

        var png = Classroom.Thumbnail(item.Program)!;
        using var bitmap = SkiaSharp.SKBitmap.Decode(png);
        Assert.Equal(300, bitmap.Width);
        int inked = 0;
        for (int x = 0; x < bitmap.Width; x += 2)
            for (int y = 0; y < bitmap.Height; y += 2)
                if (bitmap.GetPixel(x, y).Red < 120) inked++;
        Assert.True(inked > 20, "the square is in the picture");

        var music = new BlockProgram();
        music.AddStack(0, 0, BlockCatalog.MusicLoop, BlockCatalog.Note);
        Assert.Null(Classroom.Thumbnail(music));                  // not run: it would make sound
    }
}
