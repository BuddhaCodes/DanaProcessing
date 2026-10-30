using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace DanaProcessing.AvaloniaHost.Attach
{
    /// <summary>
    /// Finds the window an attached sketch follows and reads where it is.
    /// Polling, the same way Abyss Tide does it: cheap enough to call every
    /// frame, and it never misses a move the way a dropped event could.
    /// </summary>
    internal static class WindowTracker
    {
        /// <summary>Where the target is, in physical screen pixels, and its monitor's scale.</summary>
        public readonly record struct Placement(int X, int Y, int Width, int Height, double Scale);

        /// <summary>
        /// The best window for <paramref name="target"/>: the foreground one if
        /// it matches, otherwise the highest matching one in the Z order
        /// (EnumWindows lists top to bottom). Zero when none is open.
        /// </summary>
        public static IntPtr Find(WindowTarget target, IntPtr ignore)
        {
            var found = FindAll(target, ignore);
            if (found.Count == 0)
                return IntPtr.Zero;
            var fg = Win32.GetForegroundWindow();
            return found.Contains(fg) ? fg : found[0];
        }

        /// <summary>Every open window matching <paramref name="target"/>, top of the Z order first (minimized and cloaked ones included).</summary>
        public static List<IntPtr> FindAll(WindowTarget target, IntPtr ignore)
        {
            var found = new List<IntPtr>();
            var sb = new StringBuilder(256);
            var names = new Dictionary<uint, string>();

            Win32.EnumWindows((hw, _) =>
            {
                if (hw == ignore || !Win32.IsWindowVisible(hw))
                    return true;
                if (Matches(hw, target, sb, names))
                    found.Add(hw);
                return true;
            }, IntPtr.Zero);
            return found;
        }

        /// <summary>Does this one window match <paramref name="target"/>? (For WinEvent callbacks: one window at a time.)</summary>
        public static bool MatchesTarget(IntPtr hw, WindowTarget target) =>
            Matches(hw, target, new StringBuilder(256), new Dictionary<uint, string>());

        private static bool Matches(IntPtr hw, WindowTarget target, StringBuilder sb, Dictionary<uint, string> names)
        {
            if (target.ClassName != null)
            {
                sb.Length = 0;
                Win32.GetClassName(hw, sb, sb.Capacity);
                if (!string.Equals(sb.ToString(), target.ClassName, StringComparison.Ordinal))
                    return false;
            }

            if (target.ProcessName != null)
            {
                // Main windows only: no owner, not a tool window, a real size.
                if (Win32.GetWindow(hw, Win32.GW_OWNER) != IntPtr.Zero)
                    return false;
                long ex = Win32.GetWindowLongPtr(hw, Win32.GWL_EXSTYLE).ToInt64();
                if ((ex & Win32.WS_EX_TOOLWINDOW) != 0)
                    return false;
                if (!Win32.GetWindowRect(hw, out var r) || r.Right - r.Left < 50 || r.Bottom - r.Top < 50)
                    return false;

                Win32.GetWindowThreadProcessId(hw, out uint pid);
                if (!names.TryGetValue(pid, out var name))
                {
                    try { using var p = Process.GetProcessById((int)pid); name = p.ProcessName; }
                    catch { name = ""; }
                    names[pid] = name;
                }
                if (!string.Equals(name, target.ProcessName, StringComparison.OrdinalIgnoreCase))
                    return false;
            }
            return true;
        }

        /// <summary>Still open?</summary>
        public static bool Exists(IntPtr hw) => hw != IntPtr.Zero && Win32.IsWindow(hw);

        /// <summary>
        /// On screen right now: not minimized, not hidden, and not "cloaked"
        /// (on another virtual desktop, or mid-animation).
        /// </summary>
        public static bool IsShowing(IntPtr hw)
        {
            if (!Win32.IsWindowVisible(hw) || Win32.IsIconic(hw))
                return false;
            Win32.DwmGetWindowAttribute(hw, Win32.DWMWA_CLOAKED, out int cloaked, sizeof(int));
            return cloaked == 0;
        }

        /// <summary>
        /// The target's visible bounds minus <paramref name="topInsetLogical"/>
        /// at the top. DWM's extended frame bounds, not GetWindowRect: on
        /// Windows 10/11 the latter includes an invisible resize border/shadow.
        /// </summary>
        public static Placement Measure(IntPtr hw, int topInsetLogical)
        {
            if (Win32.DwmGetWindowAttribute(hw, Win32.DWMWA_EXTENDED_FRAME_BOUNDS, out Win32.RECT rc,
                    Marshal.SizeOf<Win32.RECT>()) != 0)
                Win32.GetWindowRect(hw, out rc);

            uint dpi = 96;
            try { dpi = Win32.GetDpiForWindow(hw); } catch { /* pre-1607 Windows */ }
            if (dpi == 0)
                dpi = 96;
            double scale = dpi / 96.0;

            int inset = (int)Math.Round(topInsetLogical * scale);
            int height = rc.Bottom - rc.Top - inset;
            return new Placement(rc.Left, rc.Top + inset, rc.Right - rc.Left, height, scale);
        }

        /// <summary>DWM's visible bounds of a window (GetWindowRect if DWM can't say).</summary>
        public static Win32.RECT Bounds(IntPtr hw)
        {
            if (Win32.DwmGetWindowAttribute(hw, Win32.DWMWA_EXTENDED_FRAME_BOUNDS, out Win32.RECT rc, Marshal.SizeOf<Win32.RECT>()) != 0)
                Win32.GetWindowRect(hw, out rc);
            return rc;
        }

        /// <summary>
        /// Is the target hidden behind the window in front? True only when that
        /// window is above the target, showing, and covers all of it (a
        /// maximized browser over the terminal, say). Then nobody can see the
        /// sketch and eco mode may slow it down. A terminal that merely isn't
        /// in front -- next to the window you're using, on another monitor --
        /// is still seen, so it isn't covered.
        /// </summary>
        public static bool IsCovered(IntPtr target, IntPtr foreground)
        {
            if (foreground == IntPtr.Zero || foreground == target || !IsShowing(foreground) || !IsShowing(target))
                return false;
            if (!Covers(Bounds(foreground), Bounds(target)))
                return false;
            // Above it in the Z order (the desktop, at the bottom, is "in front" when you click it).
            for (var w = Win32.GetWindow(target, Win32.GW_HWNDPREV); w != IntPtr.Zero; w = Win32.GetWindow(w, Win32.GW_HWNDPREV))
                if (w == foreground)
                    return true;
            return false;
        }

        /// <summary>Whether <paramref name="outer"/> contains all of <paramref name="inner"/>. Pure, so it's testable.</summary>
        public static bool Covers(Win32.RECT outer, Win32.RECT inner) =>
            outer.Left <= inner.Left && outer.Top <= inner.Top && outer.Right >= inner.Right && outer.Bottom >= inner.Bottom
            && inner.Right > inner.Left && inner.Bottom > inner.Top;

        /// <summary>The cursor in physical screen pixels, or null if it can't be read (secure desktop).</summary>
        public static (int X, int Y)? Cursor() =>
            Win32.GetCursorPos(out var p) ? (p.X, p.Y) : null;
    }
}
