using DanaProcessing.AvaloniaHost.Attach;
using Xunit;

namespace DanaProcessing.Tests.Core;

/// <summary>The pure parts of the attached sketch's input hooks: key mapping and screen → sketch coordinates.</summary>
public class AttachedInputMappingTests
{
    private static char NoChar(int vk) => '\0';

    [Theory]
    [InlineData(KeyMapping.VK_UP, Sketch.UP)]
    [InlineData(KeyMapping.VK_DOWN, Sketch.DOWN)]
    [InlineData(KeyMapping.VK_LEFT, Sketch.LEFT)]
    [InlineData(KeyMapping.VK_RIGHT, Sketch.RIGHT)]
    [InlineData(KeyMapping.VK_LSHIFT, Sketch.SHIFT)]
    [InlineData(KeyMapping.VK_RCONTROL, Sketch.CONTROL)]
    [InlineData(KeyMapping.VK_MENU, Sketch.ALT)]
    public void Arrows_and_modifiers_are_coded(int vk, int keyCode)
    {
        Assert.Equal((Sketch.CODED, keyCode), KeyMapping.Map(vk, _ => 'x'));
    }

    [Theory]
    [InlineData(KeyMapping.VK_RETURN, Sketch.ENTER)]
    [InlineData(KeyMapping.VK_BACK, Sketch.BACKSPACE)]
    [InlineData(KeyMapping.VK_TAB, Sketch.TAB)]
    [InlineData(KeyMapping.VK_ESCAPE, Sketch.ESC)]
    [InlineData(KeyMapping.VK_DELETE, Sketch.DELETE)]
    public void Special_keys_use_processings_chars(int vk, char key)
    {
        Assert.Equal(key, KeyMapping.Map(vk, NoChar).Key);
    }

    [Fact]
    public void Printable_keys_take_the_layouts_character_and_keep_the_vk_as_code()
    {
        Assert.Equal(('A', 0x41), KeyMapping.Map(0x41, _ => 'A'));   // Shift+A
        Assert.Equal(('ñ', 0xC0), KeyMapping.Map(0xC0, _ => 'ñ'));   // Spanish layout
        Assert.Equal((' ', 0x20), KeyMapping.Map(0x20, _ => ' '));
    }

    [Fact]
    public void Keys_that_type_nothing_are_coded_with_the_vk()
    {
        Assert.Equal((Sketch.CODED, 0x70), KeyMapping.Map(0x70, NoChar));       // F1
        Assert.Equal((Sketch.CODED, 0x43), KeyMapping.Map(0x43, _ => '\u0003')); // a control char
    }

    [Fact]
    public void Screen_points_become_logical_sketch_coordinates()
    {
        var area = new Area(X: 100, Y: 200, Width: 300, Height: 150, Scale: 1.5);
        Assert.True(InputGeometry.TryLocal(250, 260, area, out float x, out float y));
        Assert.Equal(100f, x);
        Assert.Equal(40f, y);
    }

    [Theory]
    [InlineData(99, 250)]   // left of it
    [InlineData(400, 250)]  // right edge is exclusive
    [InlineData(150, 199)]  // above (the tab bar)
    [InlineData(150, 350)]  // bottom edge is exclusive
    public void Points_outside_the_area_are_rejected_but_still_converted(int px, int py)
    {
        var area = new Area(100, 200, 300, 150, 1);
        Assert.False(InputGeometry.TryLocal(px, py, area, out float x, out _));
        Assert.Equal(px - 100, x);
    }
}
