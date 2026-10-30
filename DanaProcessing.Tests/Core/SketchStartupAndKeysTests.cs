using Avalonia.Input;
using DanaProcessing.AvaloniaHost;
using Xunit;

namespace DanaProcessing.Tests.Core;

/// <summary>
/// Two host behaviors every host shares: Settings() runs before Setup(), and a
/// key going down fires KeyPressed() and then KeyTyped() for keys that type
/// something -- plus the canvas's Avalonia key mapping (arrows, Shift, layout).
/// </summary>
public class SketchStartupAndKeysTests
{
    private class Recorder : Sketch
    {
        public readonly List<string> Log = new();
        public bool ThrowInSettings;
        public override void Settings()
        {
            Log.Add("settings");
            if (ThrowInSettings) throw new InvalidOperationException("boom");
            Size(320, 200);
        }
        public override void Setup() => Log.Add($"setup {Width}x{Height}");
        public override void Draw() { }
        public override void KeyPressed() => Log.Add($"pressed {Key}");
        public override void KeyTyped() => Log.Add($"typed {Key}");
    }

    /// <summary>The canvas's RunSafely in miniature: stops running user code after the first exception.</summary>
    private static Action<Action, string> SafeRunner(List<string> errors)
    {
        bool crashed = false;
        return (code, name) =>
        {
            if (crashed) return;
            try { code(); }
            catch (Exception) { crashed = true; errors.Add(name); }
        };
    }

    [Fact]
    public void Settings_runs_before_Setup_so_Size_there_is_already_in_effect()
    {
        var s = new Recorder();
        s.RunStartup((a, _) => a());
        Assert.Equal(new[] { "settings", "setup 320x200" }, s.Log);
    }

    [Fact]
    public void A_throwing_Settings_keeps_Setup_from_running()
    {
        var s = new Recorder { ThrowInSettings = true };
        var errors = new List<string>();
        s.RunStartup(SafeRunner(errors));
        Assert.Equal(new[] { "settings" }, s.Log);
        Assert.Equal(new[] { "Settings" }, errors);
    }

    [Theory]
    [InlineData('a', true)]
    [InlineData(' ', true)]
    [InlineData(Sketch.ENTER, true)]
    [InlineData(Sketch.CODED, false)]
    [InlineData('\0', false)]
    public void Key_down_fires_pressed_and_typed_only_for_keys_that_type(char key, bool typed)
    {
        var s = new Recorder { Key = key };
        s.RaiseKeyDown();
        var expected = typed ? new[] { $"pressed {key}", $"typed {key}" } : new[] { $"pressed {key}" };
        Assert.Equal(expected, s.Log);
    }

    [Theory]
    [InlineData(Key.Up, Sketch.UP)]
    [InlineData(Key.Down, Sketch.DOWN)]
    [InlineData(Key.Left, Sketch.LEFT)]
    [InlineData(Key.Right, Sketch.RIGHT)]
    [InlineData(Key.LeftShift, Sketch.SHIFT)]
    [InlineData(Key.RightCtrl, Sketch.CONTROL)]
    [InlineData(Key.LeftAlt, Sketch.ALT)]
    public void Canvas_arrows_and_modifiers_are_coded(Key key, int code)
    {
        Assert.Equal((Sketch.CODED, code), CanvasKeyMapping.Map(key, null));
    }

    [Theory]
    [InlineData(Key.Enter, Sketch.ENTER)]
    [InlineData(Key.Back, Sketch.BACKSPACE)]
    [InlineData(Key.Tab, Sketch.TAB)]
    [InlineData(Key.Escape, Sketch.ESC)]
    [InlineData(Key.Delete, Sketch.DELETE)]
    public void Canvas_special_keys_use_processings_chars(Key key, char expected)
    {
        Assert.Equal(expected, CanvasKeyMapping.Map(key, "\r").Key);
    }

    [Fact]
    public void Canvas_uses_the_typed_symbol_so_shift_and_layout_count()
    {
        Assert.Equal(('A', 65), CanvasKeyMapping.Map(Key.A, "A"));
        Assert.Equal(('a', 65), CanvasKeyMapping.Map(Key.A, "a"));
        Assert.Equal(('ñ', (int)'Ñ'), CanvasKeyMapping.Map(Key.Oem3, "ñ"));
        Assert.Equal(('@', (int)'@'), CanvasKeyMapping.Map(Key.D2, "@"));
    }

    [Fact]
    public void Canvas_falls_back_when_there_is_no_symbol()
    {
        Assert.Equal(('r', (int)'R'), CanvasKeyMapping.Map(Key.R, null));
        Assert.Equal(('7', (int)'7'), CanvasKeyMapping.Map(Key.NumPad7, null));
        Assert.Equal(('a', (int)'A'), CanvasKeyMapping.Map(Key.A, "\u0001")); // Ctrl+A
        Assert.Equal(('\0', 0), CanvasKeyMapping.Map(Key.F5, null));
    }
}
