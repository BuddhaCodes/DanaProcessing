using System.Runtime.InteropServices;
using DanaProcessing.AvaloniaHost.Attach;
using DanaProcessing.Ide.Export;
using Xunit;

namespace DanaProcessing.Tests.Core;

/// <summary>
/// One sketch per Windows Terminal pane: finding the panes (UI Automation),
/// keeping each sketch on "its" pane while splits and dividers change, and
/// placing it as the window moves and resizes.
/// </summary>
public class TerminalPanesTests
{
    private static Win32.RECT R(int l, int t, int r, int b) => new() { Left = l, Top = t, Right = r, Bottom = b };
    private static PaneInfo P(string name, int l, int t, int r, int b) => new(name, R(l, t, r, b));

    [Fact]
    public void Panes_are_numbered_left_to_right_then_top_to_bottom()
    {
        var ordered = PaneLayout.Order(new[] { P("c", 500, 300, 900, 600), P("a", 0, 0, 500, 600), P("b", 500, 0, 900, 300) });
        Assert.Equal(new[] { "a", "b", "c" }, ordered.Select(p => p.Name));
    }

    [Fact]
    public void Only_the_profiles_panes_get_a_sketch()
    {
        var panes = new[] { P("MiSketch", 0, 0, 500, 600), P("Windows PowerShell", 500, 0, 900, 600), P("MiSketch", 900, 0, 1200, 600) };
        Assert.Equal(2, PaneFilter.ForProfile(panes, "MiSketch").Count);
        Assert.Equal(3, PaneFilter.ForProfile(panes, null).Count);
    }

    [Fact]
    public void A_pane_keeps_its_place_as_the_window_moves_and_resizes()
    {
        var window = R(100, 100, 1100, 700);                 // 1000 x 600
        var content = R(100, 140, 1100, 700);                // both panes, under a 40 px tab bar
        var pane = R(600, 140, 1100, 700);                   // the right half
        var f = PaneLayout.ToFraction(pane, window, content);
        Assert.Equal(pane, PaneLayout.FromFraction(f, window));

        var moved = PaneLayout.FromFraction(f, R(300, 50, 1300, 650));
        Assert.Equal(R(800, 90, 1300, 650), moved);           // same pane, window moved

        // Window twice as big: the half grows with it, the tab bar stays 40 px
        // (Windows Terminal splits the content by percentage, not the window).
        var bigger = PaneLayout.FromFraction(f, R(100, 100, 2100, 1300));
        Assert.Equal(R(1100, 140, 2100, 1300), bigger);
    }

    [Fact]
    public void Stacked_panes_stay_under_the_tab_bar_while_resizing()
    {
        var window = R(0, 0, 800, 640);
        var panes = new[] { P("a", 8, 40, 792, 340), P("b", 8, 340, 792, 632) };
        var content = PaneLayout.Union(panes);
        Assert.Equal(R(8, 40, 792, 632), content);
        var top = PaneLayout.ToFraction(panes[0].Bounds, window, content);
        var bottom = PaneLayout.ToFraction(panes[1].Bounds, window, content);

        var taller = R(0, 0, 800, 1232);                      // 592 px more content
        var t = PaneLayout.FromFraction(top, taller);
        var b = PaneLayout.FromFraction(bottom, taller);
        Assert.Equal(40, t.Top);                              // the tab bar doesn't grow
        Assert.Equal(t.Bottom, b.Top);                        // no gap or overlap at the divider
        Assert.Equal(1224, b.Bottom);                         // 8 px border at the bottom, as before
        Assert.InRange(t.Bottom, 638, 642);                   // ~ the same proportion (300 of 592)
    }

    [Fact]
    public void A_fast_drag_keeps_every_sketch_on_its_own_pane()
    {
        // Two panes; between two scans the window was dragged 700 px right --
        // further than a pane is wide, so in screen pixels nothing overlaps.
        var before = R(100, 100, 1100, 700);
        var after = R(800, 100, 1800, 700);
        var old = new[] { R(108, 140, 600, 692), R(600, 140, 1092, 692) }.Select(r => PaneLayout.Relative(r, before)).ToList();
        var now = new[] { R(808, 140, 1300, 692), R(1300, 140, 1792, 692) }.Select(r => PaneLayout.Relative(r, after)).ToList();
        Assert.Equal(new[] { 0, 1 }, PaneLayout.Match(old, now));
        Assert.Equal(R(8, 40, 500, 592), now[0]);
    }

    [Fact]
    public void Pane_overlays_do_not_fight_for_the_spot_next_to_the_terminal()
    {
        // Z-order, top to bottom: tooltip (hidden), terminal, overlay A, overlay B, browser.
        IntPtr tooltip = 1, terminal = 2, a = 3, b = 4, browser = 5;
        var stack = new[] { tooltip, terminal, a, b, browser };
        IntPtr Prev(IntPtr h) { int i = Array.IndexOf(stack, h); return i > 0 ? stack[i - 1] : IntPtr.Zero; }
        bool Overlay(IntPtr h) => h == a || h == b;

        // B is below A, which is below the terminal: both are "right below it", nobody moves.
        Assert.True(ZOrder.Reaches(a, terminal, Prev, h => h != a && Overlay(h)));
        Assert.True(ZOrder.Reaches(b, terminal, Prev, h => h != b && Overlay(h)));
        // A real window in between does count.
        stack = new[] { terminal, browser, a };
        Assert.False(ZOrder.Reaches(a, terminal, Prev, h => h != a && Overlay(h)));
        // Above: a hidden tooltip between the terminal and us doesn't count either.
        stack = new[] { a, tooltip, terminal, browser };
        Assert.True(ZOrder.Reaches(terminal, a, Prev, h => h != a && (h == tooltip || Overlay(h))));
        Assert.False(ZOrder.Reaches(terminal, a, Prev, h => h != a && Overlay(h)));   // a visible window would
    }

    [Fact]
    public void No_ui_automation_while_the_mouse_is_on_the_tab_bar()
    {
        var window = R(100, 100, 1100, 700);
        var panes = R(1, 41, 999, 599);                       // relative: under a 40 px tab bar
        Assert.True(PaneHold.CursorOverChrome((300, 120), window, panes));    // on a tab
        Assert.True(PaneHold.CursorOverChrome((1090, 110), window, panes));   // the close button
        Assert.False(PaneHold.CursorOverChrome((300, 400), window, panes));   // over a pane
        Assert.False(PaneHold.CursorOverChrome((50, 120), window, panes));    // outside the window
        Assert.False(PaneHold.CursorOverChrome(null, window, panes));         // cursor unknown
    }

    [Fact]
    public void A_terminal_menu_in_front_keeps_the_panes_where_they_are()
    {
        // The tab's context menu is open (a popup of the terminal's in front): hold.
        Assert.True(PaneHold.Hold(visiblePanes: 0, hadPanes: true, "MiSketch", "MiSketch", ownPopupInFront: true));
        Assert.True(PaneHold.Hold(visiblePanes: 2, hadPanes: true, "MiSketch", "MiSketch", ownPopupInFront: true));
        // Panes vanished, same tab (title unchanged): UI Automation glitch / flyout -> hold.
        Assert.True(PaneHold.Hold(0, true, "MiSketch", "MiSketch", false));
        // Another tab in front (the title changed): really hidden.
        Assert.False(PaneHold.Hold(0, true, "Configuración", "MiSketch", false));
        // Panes on screen and no menu: scan normally.
        Assert.False(PaneHold.Hold(2, true, "MiSketch", "MiSketch", false));
        // Nothing to hold yet.
        Assert.False(PaneHold.Hold(0, false, "MiSketch", "MiSketch", true));
    }

    [Fact]
    public void A_pane_slot_remembers_when_it_had_the_focus()
    {
        var slot = new PaneSlot(new IntPtr(1), new PaneFraction(0, 0, 1, 1), R(0, 0, 10, 10));
        Assert.Equal(0, slot.FocusedAt);
        slot.FocusedAt = 12.5;
        slot.Update(new PaneFraction(0.5, 0, 0.5, 1), R(5, 0, 10, 10));
        Assert.Equal(12.5, slot.FocusedAt);                   // moving it doesn't forget it
    }

    [Fact]
    public void Splitting_keeps_the_sketch_on_the_part_that_stayed()
    {
        // One pane, then split vertically: the left half is the old pane shrunk, the right one is new.
        var old = new[] { R(0, 40, 1000, 600) };
        var now = new[] { R(0, 40, 500, 600), R(500, 40, 1000, 600) };
        Assert.Equal(new[] { 0, -1 }, PaneLayout.Match(old, now));
    }

    [Fact]
    public void Moving_a_divider_or_closing_a_pane_keeps_the_others()
    {
        var old = new[] { R(0, 40, 500, 600), R(500, 40, 1000, 600) };
        Assert.Equal(new[] { 0, 1 }, PaneLayout.Match(old, new[] { R(0, 40, 700, 600), R(700, 40, 1000, 600) }));   // divider moved right
        Assert.Contains(PaneLayout.Match(old, new[] { R(0, 40, 1000, 600) })[0], new[] { 0, 1 });            // one closed: the other carries on
        Assert.Equal(new[] { -1 }, PaneLayout.Match(old, new[] { R(2000, 0, 2500, 500) }));   // somewhere else entirely: new
        Assert.Empty(PaneLayout.Match(old, Array.Empty<Win32.RECT>()));
    }

    [Fact]
    public void Each_old_pane_is_used_once()
    {
        var old = new[] { R(0, 0, 1000, 1000) };
        var now = new[] { R(0, 0, 600, 1000), R(400, 0, 1000, 1000) };   // both overlap the old one
        var m = PaneLayout.Match(old, now);
        Assert.Equal(1, m.Count(i => i == 0));
        Assert.Equal(1, m.Count(i => i == -1));
    }

    [Fact]
    public void A_pane_slot_hides_and_comes_back()
    {
        var slot = new PaneSlot(new IntPtr(1), new PaneFraction(0, 0, 1, 1), R(0, 0, 10, 10));
        Assert.True(slot.Visible);
        slot.Hide();
        Assert.False(slot.Visible);
        Assert.Equal(R(0, 0, 10, 10), slot.LastBounds);
        slot.Update(new PaneFraction(0.5, 0, 0.5, 1), R(5, 0, 10, 10));
        Assert.True(slot.Visible);
        Assert.Equal(0.5, slot.Place.X);
    }

    [Fact]
    public void One_sketch_per_terminal_means_per_pane_too()
    {
        Assert.True(ExportedSketchRunner.BuildOverrides(eachWindow: true, profileBackdrop: null).EachPane);
        Assert.False(ExportedSketchRunner.BuildOverrides(eachWindow: false, profileBackdrop: null).EachPane);
    }

    [Fact]
    public void Ui_automation_slots_follow_the_sdk_header()
    {
        Assert.Equal((6, 23), (TerminalPanes.Slot.Automation_ElementFromHandle, TerminalPanes.Slot.Automation_CreatePropertyCondition));
        Assert.Equal((6, 23, 26, 38, 43), (TerminalPanes.Slot.Element_FindAll, TerminalPanes.Slot.Element_CurrentName,
                                            TerminalPanes.Slot.Element_CurrentHasKeyboardFocus,
                                            TerminalPanes.Slot.Element_CurrentIsOffscreen, TerminalPanes.Slot.Element_CurrentBoundingRectangle));
        Assert.Equal((3, 4), (TerminalPanes.Slot.Array_Length, TerminalPanes.Slot.Array_GetElement));
    }

    /// <summary>
    /// On Windows, for real: UI Automation answers through those slots (a list,
    /// maybe empty, for the shell's window), and every open Windows Terminal
    /// window has at least one pane with a name. Elsewhere it passes.
    /// </summary>
    [Fact]
    public void Finds_the_panes_through_ui_automation_on_windows()
    {
        if (!OperatingSystem.IsWindows() || RuntimeInformation.ProcessArchitecture is not (Architecture.X64 or Architecture.Arm64))
            return;
        using var panes = new TerminalPanes();
        Assert.NotNull(panes.Find(GetShellWindow()));
        foreach (var hw in WindowTracker.FindAll(WindowTarget.Terminal, IntPtr.Zero))
        {
            if (!WindowTracker.IsShowing(hw))
                continue;
            var found = panes.Find(hw);
            Assert.NotNull(found);
            Assert.NotEmpty(found!);
            Assert.All(found!, p => Assert.False(string.IsNullOrEmpty(p.Name)));
        }
    }

    [DllImport("user32.dll")] private static extern IntPtr GetShellWindow();
}
