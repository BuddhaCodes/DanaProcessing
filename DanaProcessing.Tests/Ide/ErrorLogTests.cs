using DanaProcessing.Ide.CrashReporting;
using Xunit;

namespace DanaProcessing.Tests.Ide;

/// <summary>The always-on log of unhandled exceptions: the whole stack, appended, never throwing.</summary>
public class ErrorLogTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "dana-errorlog-" + Guid.NewGuid().ToString("N"));
    private string LogPath => Path.Combine(_dir, "logs", "errores.log");

    public void Dispose()
    {
        try { Directory.Delete(_dir, true); } catch { }
    }

    private static Exception Thrown()
    {
        try
        {
            try { new List<int>()[3] = 1; }
            catch (Exception inner) { throw new InvalidOperationException("outer", inner); }
        }
        catch (Exception ex) { return ex; }
        return null!;
    }

    [Fact]
    public void Writes_the_type_message_stack_and_inner_exception()
    {
        ErrorLog.Write(LogPath, Thrown(), "Dispatcher.UIThread.UnhandledException", new DateTime(2026, 10, 25, 23, 5, 1));
        string text = File.ReadAllText(LogPath);
        Assert.Contains("2026-10-25 23:05:01", text);
        Assert.Contains("Dispatcher.UIThread.UnhandledException", text);
        Assert.Contains("System.InvalidOperationException: outer", text);
        Assert.Contains("System.ArgumentOutOfRangeException", text);   // the inner one, with its own stack
        Assert.Contains(nameof(Thrown), text);
    }

    [Fact]
    public void Appends_and_rolls_over_when_it_gets_big()
    {
        var ex = Thrown();
        ErrorLog.Write(LogPath, ex, "a", DateTime.Now);
        ErrorLog.Write(LogPath, ex, "b", DateTime.Now);
        Assert.Equal(2, File.ReadAllText(LogPath).Split("=====").Length - 1);

        File.AppendAllText(LogPath, new string('x', 600 * 1024));
        ErrorLog.Write(LogPath, ex, "c", DateTime.Now);
        Assert.True(File.Exists(Path.ChangeExtension(LogPath, ".old.log")));
        Assert.Equal(1, File.ReadAllText(LogPath).Split("=====").Length - 1);
    }

    [Fact]
    public void Never_throws_even_when_it_cannot_write()
    {
        string blocked = Path.Combine(_dir, "file-not-folder");
        Directory.CreateDirectory(_dir);
        File.WriteAllText(blocked, "");
        ErrorLog.Write(Path.Combine(blocked, "errores.log"), Thrown(), "x", DateTime.Now);   // its "folder" is a file
    }
}
