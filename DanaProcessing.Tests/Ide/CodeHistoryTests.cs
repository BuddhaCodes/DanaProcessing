using DanaProcessing.Ide.Editor;
using Xunit;

namespace DanaProcessing.Tests.Ide;

/// <summary>The code time machine's store: versions per sketch, no duplicates, pruning, thumbnails, diffs.</summary>
public class CodeHistoryTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "dana_history_" + Guid.NewGuid().ToString("N"));
    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    [Fact]
    public void Every_run_is_kept_newest_first_with_its_seed()
    {
        var h = new CodeHistory(_root);
        var t0 = new DateTime(2026, 10, 16, 12, 0, 0, DateTimeKind.Utc);
        h.Record("a", "v1", 11, t0);
        h.Record("a", "v2", 22, t0.AddMinutes(1));
        h.Record("b", "other sketch", 33, t0);

        var list = h.List("a");
        Assert.Equal(2, list.Count);
        Assert.Equal("v2", list[0].ReadCode());
        Assert.Equal(22, list[0].Seed);
        Assert.Equal(t0.AddMinutes(1), list[0].TimeUtc);
        Assert.Single(h.List("b"));
    }

    [Fact]
    public void Running_the_same_code_again_does_not_add_a_version()
    {
        var h = new CodeHistory(_root);
        var first = h.Record("a", "same", 1, DateTime.UtcNow)!;
        var again = h.Record("a", "same", 2, DateTime.UtcNow.AddSeconds(5))!;
        Assert.Equal(first.Id, again.Id);
        Assert.Single(h.List("a"));
    }

    [Fact]
    public void Only_the_newest_versions_are_kept()
    {
        var h = new CodeHistory(_root);
        var t0 = DateTime.UtcNow;
        for (int i = 0; i < CodeHistory.MaxVersions + 5; i++)
            h.Record("a", $"v{i}", i, t0.AddSeconds(i));
        var list = h.List("a");
        Assert.Equal(CodeHistory.MaxVersions, list.Count);
        Assert.Equal($"v{CodeHistory.MaxVersions + 4}", list[0].ReadCode());
    }

    [Fact]
    public void A_thumbnail_sticks_to_its_version()
    {
        var h = new CodeHistory(_root);
        var v = h.Record("a", "code", 7)!;
        Assert.Null(v.ThumbnailPath);
        h.AttachThumbnail(v, new byte[] { 0xFF, 0xD8, 0xFF });
        Assert.NotNull(h.List("a")[0].ThumbnailPath);
    }

    [Fact]
    public void Keys_are_per_file_and_stable()
    {
        Assert.Equal("untitled", CodeHistory.KeyFor(null));
        var k1 = CodeHistory.KeyFor("/tmp/flowers/Flowers.cs");
        Assert.StartsWith("Flowers-", k1);
        Assert.Equal(k1, CodeHistory.KeyFor("/tmp/flowers/Flowers.cs"));
        Assert.NotEqual(k1, CodeHistory.KeyFor("/tmp/other/Flowers.cs"));
    }

    [Fact]
    public void Line_diff_counts_added_and_removed_lines()
    {
        Assert.Equal((0, 0), CodeHistory.LineDiff("a\nb\nc", "a\nb\nc"));
        Assert.Equal((1, 0), CodeHistory.LineDiff("a\nc", "a\nb\nc"));
        Assert.Equal((0, 1), CodeHistory.LineDiff("a\nb\nc", "a\nc"));
        Assert.Equal((1, 1), CodeHistory.LineDiff("a\nb\nc", "a\nB\nc"));
        Assert.Equal((0, 0), CodeHistory.LineDiff("a  \r\nb", "a\nb")); // line endings and trailing spaces don't count
    }
}
