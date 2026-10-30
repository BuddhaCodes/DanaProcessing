using DanaProcessing.AvaloniaHost.Attach;
using Xunit;

namespace DanaProcessing.Tests.Core;

/// <summary>The diagnostic trace of attached sketches: off without its flag file, one line per call, never throws.</summary>
[Collection("AttachTrace")]
public class AttachTraceTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "dana-trace-" + Guid.NewGuid().ToString("N"));

    public AttachTraceTests()
    {
        Directory.CreateDirectory(_root);
        AttachTrace.RootOverride = _root;
        AttachTrace.Recheck();
    }

    public void Dispose()
    {
        AttachTrace.RootOverride = null;
        AttachTrace.Recheck();
        try { Directory.Delete(_root, recursive: true); } catch { }
    }

    [Fact]
    public void Writes_nothing_until_the_flag_file_exists()
    {
        AttachTrace.Write("test", "hidden");
        Assert.False(File.Exists(AttachTrace.LogFile));

        File.WriteAllText(AttachTrace.FlagFile, "");
        AttachTrace.Recheck();
        AttachTrace.Write("host#1", "Attached -> TargetHidden");
        AttachTrace.Write("fleet", "pane gone");
        var lines = File.ReadAllLines(AttachTrace.LogFile);
        Assert.Equal(2, lines.Length);
        Assert.Contains("host#1: Attached -> TargetHidden", lines[0]);
    }

    [Fact]
    public void Lines_have_time_thread_and_place()
    {
        var line = AttachTrace.FormatLine(new DateTime(2026, 10, 26, 18, 26, 40, 123), 7, "zorder", "below");
        Assert.Equal("18:26:40.123 [  7] zorder: below", line);
        Assert.Equal("(1,2)-(3,4)", AttachTrace.Rect(new Win32.RECT { Left = 1, Top = 2, Right = 3, Bottom = 4 }));
        Assert.Equal("0", AttachTrace.Describe(IntPtr.Zero));
    }
}
