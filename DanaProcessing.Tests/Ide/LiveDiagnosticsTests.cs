using System.Reflection;
using DanaProcessing.Ide.Editor;
using Xunit;

namespace DanaProcessing.Tests.Ide;

/// <summary>
/// Live errors are shown in one place -- the bottom "Live errors" tab (plus the
/// squiggles) -- and only once you stop typing, so a half-written line doesn't
/// flash an error at you.
/// </summary>
public class LiveDiagnosticsTests
{
    [Fact]
    public void The_editor_waits_for_a_real_pause_before_checking_again()
    {
        Assert.InRange(SketchEditorView.LiveDiagnosticsDelay.TotalMilliseconds, 1000, 2000);
    }

    [Fact]
    public void There_is_no_error_banner_over_the_code()
    {
        var fields = typeof(SketchEditorView).GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        Assert.DoesNotContain(fields, f => f.Name.Contains("Banner", StringComparison.OrdinalIgnoreCase));
    }
}
