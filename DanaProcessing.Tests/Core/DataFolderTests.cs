using DanaProcessing.Ide.Export;
using DanaProcessing.Tests.Support;
using SkiaSharp;
using Xunit;

namespace DanaProcessing.Tests.Core;

/// <summary>The sketch folder and data/: relative paths that work the same in the IDE and in an exported app.</summary>
public class DataFolderTests : IDisposable
{
    private readonly string _sketch = Path.Combine(Path.GetTempPath(), "dana_sketch_" + Guid.NewGuid().ToString("N"));
    private string Data => Path.Combine(_sketch, "data");

    public DataFolderTests()
    {
        Directory.CreateDirectory(Path.Combine(Data, "maps"));
        File.WriteAllLines(Path.Combine(Data, "words.txt"), new[] { "uno", "dos" });
        File.WriteAllText(Path.Combine(Data, "maps", "city.csv"), "name,pop\nHavana,2100000\n");
        File.WriteAllText(Path.Combine(_sketch, "words.txt"), "this one is NOT in data/");
        File.WriteAllText(Path.Combine(_sketch, "notes.txt"), "sketch folder");

        using var surface = SKSurface.Create(new SKImageInfo(8, 6));
        surface.Canvas.Clear(SKColors.Orange);
        using var png = surface.Snapshot().Encode(SKEncodedImageFormat.Png, 100);
        File.WriteAllBytes(Path.Combine(Data, "dot.png"), png.ToArray());
    }

    public void Dispose() => Directory.Delete(_sketch, recursive: true);

    public class Reader : Sketch
    {
        public override void Draw() { }
    }

    private Reader NewSketch()
    {
        var s = new Reader();
        s.SetSketchFolder(_sketch);
        return s;
    }

    [Fact]
    public void Relative_reads_look_in_data_first_then_the_sketch_folder()
    {
        var s = NewSketch();
        Assert.Equal(new[] { "uno", "dos" }, s.LoadStrings("words.txt"));          // data/ wins
        Assert.Equal("sketch folder", s.LoadStrings("notes.txt")[0]);               // then the sketch folder
        Assert.Equal("Havana", s.LoadTable("maps/city.csv", "header").GetRow(0).GetString("name"));
        var img = s.LoadImage("dot.png");
        Assert.Equal((8, 6), (img.Width, img.Height));
        Assert.Equal(2, s.LoadBytes("words.txt").Count(b => b == (byte)'\n'));
    }

    [Fact]
    public void Absolute_paths_are_left_alone_and_missing_files_name_what_was_asked()
    {
        var s = NewSketch();
        var absolute = Path.Combine(_sketch, "notes.txt");
        Assert.Equal("sketch folder", s.LoadStrings(absolute)[0]);
        var ex = Assert.ThrowsAny<IOException>(() => s.LoadStrings("nope.txt"));
        Assert.Contains("nope.txt", ex.Message);
    }

    [Fact]
    public void Relative_writes_go_into_the_sketch_folder()
    {
        var s = NewSketch();
        Offscreen.Run(s, 1);
        s.SaveStrings("out/log.txt", new[] { "hola" });
        Assert.Equal("hola", File.ReadAllText(Path.Combine(_sketch, "out", "log.txt")).Trim());

        s.SaveFrame("frame-##.png");
        Assert.True(File.Exists(Path.Combine(_sketch, "frame-01.png")));
    }

    [Fact]
    public void DataPath_and_SketchPath_point_at_the_folders()
    {
        var s = NewSketch();
        Assert.Equal(Path.GetFullPath(_sketch), s.SketchFolder);
        Assert.Equal(Path.Combine(Path.GetFullPath(_sketch), "data", "dot.png"), s.DataPath("dot.png"));
        Assert.Equal(Path.Combine(Path.GetFullPath(_sketch), "x.txt"), s.SketchPath("x.txt"));
    }

    [Fact]
    public void An_unsaved_sketch_keeps_paths_as_they_are()
    {
        var s = new Reader();
        Assert.Null(s.SketchFolder);
        Assert.Equal("a/b.txt", s.ResolveInputPath("a/b.txt"));
        Assert.Equal("a/b.txt", s.ResolveOutputPath("a/b.txt"));
    }

    [Fact]
    public void Hot_reload_keeps_the_sketch_folder()
    {
        var old = NewSketch();
        var fresh = new Reader();
        SketchHotReload.Transplant(old, fresh);
        Assert.Equal(old.SketchFolder, fresh.SketchFolder);
    }

    [Fact]
    public void Export_copies_data_with_its_subfolders()
    {
        var export = Path.Combine(Path.GetTempPath(), "dana_export_" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(export);
            int copied = SketchExporter.CopyDataFolder(_sketch, export);
            Assert.Equal(3, copied);
            Assert.True(File.Exists(Path.Combine(export, "data", "maps", "city.csv")));

            // The exported app's sketch, pointed at its own folder, reads the same names.
            var s = new Reader();
            s.SetSketchFolder(export);
            Assert.Equal("uno", s.LoadStrings("words.txt")[0]);
        }
        finally
        {
            Directory.Delete(export, recursive: true);
        }
    }
}
