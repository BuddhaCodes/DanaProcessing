using DanaProcessing.AvaloniaHost.Attach;
using Xunit;

namespace DanaProcessing.Tests.Core;

/// <summary>AttachedSketch: a sketch drawn on another app's window (the parts that don't need Windows).</summary>
public class AttachedSketchTests
{
    public class Rain : AttachedSketch
    {
        public int Resizes;
        public override void Draw() => Clear();
        public override void WindowResized() => Resizes++;
    }

    [Fact]
    public void Follows_Windows_Terminal_behind_it_by_default()
    {
        var s = new Rain();
        Assert.Equal("CASCADIA_HOSTING_WINDOW_CLASS", s.Target.ClassName);
        Assert.True(s.Target.IsTerminal);
        Assert.Equal(AttachLayer.Behind, s.Layer);
        Assert.Equal(40, s.TopInset);
        Assert.False(s.IsAttached);
    }

    [Fact]
    public void Attach_changes_target_layer_and_inset_and_bumps_the_version()
    {
        var s = new Rain();
        int before = s.AttachVersion;

        s.Attach(WindowTarget.Process("Code.exe"), AttachLayer.Behind);
        Assert.Equal("Code", s.Target.ProcessName);
        Assert.Null(s.Target.ClassName);
        Assert.Equal(AttachLayer.Behind, s.Layer);
        Assert.Equal(0, s.TopInset);              // the target's default
        Assert.Equal(before + 1, s.AttachVersion);

        s.Attach(WindowTarget.Terminal, topInset: 0);
        Assert.Equal(0, s.TopInset);               // explicit beats default
        Assert.Equal(before + 2, s.AttachVersion);
    }

    [Fact]
    public void Terminal_sketches_are_always_behind_other_windows_choose()
    {
        var s = new Rain();
        s.Attach(WindowTarget.Terminal, AttachLayer.Above);
        Assert.Equal(AttachLayer.Behind, s.Layer);            // never on top of the terminal

        s.Attach(WindowTarget.Class("CASCADIA_HOSTING_WINDOW_CLASS"), AttachLayer.Above);
        Assert.True(s.Target.IsTerminal);                     // same window class: still the terminal
        Assert.Equal(AttachLayer.Behind, s.Layer);

        s.Attach(WindowTarget.Process("notepad"));
        Assert.False(s.Target.IsTerminal);
        Assert.Equal(AttachLayer.Above, s.Layer);             // other windows: Above by default
        s.Attach(WindowTarget.Process("notepad"), AttachLayer.Behind);
        Assert.Equal(AttachLayer.Behind, s.Layer);
    }

    [Fact]
    public void Forcing_Above_on_the_terminal_changes_nothing()
    {
        var s = new Rain();
        int version = s.AttachVersion;
        s.ForceLayer(AttachLayer.Above);
        Assert.Equal(AttachLayer.Behind, s.Layer);
        Assert.Equal(version, s.AttachVersion);               // nothing for the host to redo
    }

    [Fact]
    public void Attached_size_sets_Width_Height_and_only_notifies_when_asked()
    {
        var s = new Rain();
        s.ApplyAttachedSize(1200, 700, notify: false);
        Assert.Equal((1200, 700), (s.Width, s.Height));
        Assert.Equal(0, s.Resizes);

        s.ApplyAttachedSize(1000, 700, notify: true);
        Assert.Equal(1000, s.Width);
        Assert.Equal(1, s.Resizes);

        s.ApplyAttachedSize(1000, 700, notify: true); // same size: nothing to say
        Assert.Equal(1, s.Resizes);
    }

    [Fact]
    public void Window_targets_reject_empty_names()
    {
        Assert.ThrowsAny<ArgumentException>(() => WindowTarget.Class(" "));
        Assert.ThrowsAny<ArgumentException>(() => WindowTarget.Process(""));
    }

    [Fact]
    public void Host_reports_unsupported_off_Windows()
    {
        if (OperatingSystem.IsWindows())
            return; // the real thing needs a desktop; covered by hand (see ATTACHED-SKETCHES.md)

        using var host = new AttachedSketchHost(new Rain(), connectsAudio: false);
        host.Start();
        Assert.Equal(AttachState.Unsupported, host.State);
    }

    [Fact]
    public void Hot_reload_on_a_host_that_is_not_running_asks_for_a_restart()
    {
        using var host = new AttachedSketchHost(new Rain(), connectsAudio: false);
        var plan = host.HotReload(new Rain());
        Assert.False(plan.CanApply);
    }
}
