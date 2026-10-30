using System.Runtime.InteropServices;

namespace DanaProcessing.AvaloniaHost.Attach
{
    /// <summary>
    /// A Windows Terminal pane on screen: its name, where it is (physical
    /// screen pixels) and whether it has the keyboard focus (the pane you type
    /// in -- the one a shell signal comes from).
    /// </summary>
    internal readonly record struct PaneInfo(string Name, Win32.RECT Bounds, bool Focused = false);

    /// <summary>
    /// The panes (split terminals) of a Windows Terminal window, through UI
    /// Automation, so an export can run one sketch per pane like the
    /// terminal's own pixel shaders do. Each pane is a TermControl, and
    /// TermControlAutomationPeer reports ClassName "TermControl" and, as its
    /// Name, the profile's tabTitle (or its name) -- so the panes of the
    /// export's own profile are the ones named after it. Only the active tab's
    /// panes are in the automation tree.
    ///
    /// COM through vtables, like DCompWindow; slot numbers follow
    /// UIAutomationClient.h. Use from one thread (the fleet's scanner). Find()
    /// returns null when UI Automation can't answer: the caller then treats the
    /// whole window as one pane, as before.
    /// </summary>
    internal sealed class TerminalPanes : IDisposable
    {
        private static readonly Guid CLSID_CUIAutomation = new("ff48dba4-60ef-4201-aa87-54103eef594e");
        private static readonly Guid IID_IUIAutomation = new("30cbe57d-d9d0-452a-ab13-7ac5ac4825ee");
        private const int UIA_ClassNamePropertyId = 30012;
        private const int TreeScope_Descendants = 4;
        private const ushort VT_BSTR = 8;

        internal static class Slot
        {
            // IUIAutomation: CompareElements 3, CompareRuntimeIds 4, GetRootElement 5, ElementFromHandle 6, ...
            // get_RawViewCondition 17 ... CreateTrueCondition 21, CreateFalseCondition 22, CreatePropertyCondition 23
            public const int Automation_ElementFromHandle = 6;
            public const int Automation_CreatePropertyCondition = 23;
            // IUIAutomationElement: SetFocus 3, GetRuntimeId 4, FindFirst 5, FindAll 6, ... get_CurrentName 23,
            // ... get_CurrentIsOffscreen 38, ... get_CurrentBoundingRectangle 43
            public const int Element_FindAll = 6;
            public const int Element_CurrentName = 23;
            public const int Element_CurrentHasKeyboardFocus = 26;
            public const int Element_CurrentIsOffscreen = 38;
            public const int Element_CurrentBoundingRectangle = 43;
            // IUIAutomationElementArray: get_Length 3, GetElement 4
            public const int Array_Length = 3;
            public const int Array_GetElement = 4;
        }

        [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int ElementFromHandleFn(IntPtr self, IntPtr hwnd, out IntPtr element);
        // VARIANT (24 bytes) goes by reference on x64 and ARM64 -- hence a pointer here (see Find's IntPtr.Size check).
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int CreatePropertyConditionFn(IntPtr self, int propertyId, IntPtr variant, out IntPtr condition);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int FindAllFn(IntPtr self, int scope, IntPtr condition, out IntPtr found);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int GetBstrFn(IntPtr self, out IntPtr bstr);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int GetBoolFn(IntPtr self, out int value);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int GetRectFn(IntPtr self, out Win32.RECT rect);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int GetIntFn(IntPtr self, out int value);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int GetElementFn(IntPtr self, int index, out IntPtr element);

        private IntPtr _automation, _condition;
        private bool _failed;

        /// <summary>
        /// The panes of <paramref name="window"/> that are on screen, left to
        /// right then top to bottom; null if UI Automation isn't available.
        /// </summary>
        public IReadOnlyList<PaneInfo>? Find(IntPtr window)
        {
            if (_failed || IntPtr.Size != 8 || !OperatingSystem.IsWindows())
                return null;
            try
            {
                if (_automation == IntPtr.Zero && !Init())
                    return null;

                if (Com.Call<ElementFromHandleFn>(_automation, Slot.Automation_ElementFromHandle)(_automation, window, out var root) < 0 || root == IntPtr.Zero)
                    return null;
                IntPtr found = IntPtr.Zero;
                try
                {
                    if (Com.Call<FindAllFn>(root, Slot.Element_FindAll)(root, TreeScope_Descendants, _condition, out found) < 0 || found == IntPtr.Zero)
                        return null;
                    Com.Call<GetIntFn>(found, Slot.Array_Length)(found, out int count);
                    var panes = new List<PaneInfo>(count);
                    for (int i = 0; i < count; i++)
                    {
                        if (Com.Call<GetElementFn>(found, Slot.Array_GetElement)(found, i, out var element) < 0 || element == IntPtr.Zero)
                            continue;
                        try
                        {
                            Com.Call<GetBoolFn>(element, Slot.Element_CurrentIsOffscreen)(element, out int offscreen);
                            if (offscreen != 0)
                                continue;
                            Com.Call<GetRectFn>(element, Slot.Element_CurrentBoundingRectangle)(element, out var rect);
                            if (rect.Right - rect.Left < 32 || rect.Bottom - rect.Top < 32)
                                continue;
                            string name = "";
                            if (Com.Call<GetBstrFn>(element, Slot.Element_CurrentName)(element, out var bstr) >= 0 && bstr != IntPtr.Zero)
                            {
                                name = Marshal.PtrToStringBSTR(bstr);
                                Marshal.FreeBSTR(bstr);
                            }
                            bool focused = Com.Call<GetBoolFn>(element, Slot.Element_CurrentHasKeyboardFocus)(element, out int focus) >= 0 && focus != 0;
                            panes.Add(new PaneInfo(name, rect, focused));
                        }
                        finally
                        {
                            Marshal.Release(element);
                        }
                    }
                    return PaneLayout.Order(panes);
                }
                finally
                {
                    if (found != IntPtr.Zero)
                        Marshal.Release(found);
                    Marshal.Release(root);
                }
            }
            catch (Exception ex)
            {
                DanaLogger.ErrorFromException(ex, "UI Automation (paneles de Windows Terminal)");
                _failed = true;
                return null;
            }
        }

        private bool Init()
        {
            Native.CoInitializeEx(IntPtr.Zero, 0); // COINIT_MULTITHREADED; S_FALSE / RPC_E_CHANGED_MODE are fine
            if (Native.CoCreateInstance(CLSID_CUIAutomation, IntPtr.Zero, 1 /* CLSCTX_INPROC_SERVER */, IID_IUIAutomation, out _automation) < 0
                || _automation == IntPtr.Zero)
            {
                _failed = true;
                return false;
            }

            // ClassName == "TermControl", as a VARIANT holding a BSTR.
            IntPtr variant = Marshal.AllocHGlobal(24);
            IntPtr bstr = Marshal.StringToBSTR("TermControl");
            try
            {
                for (int i = 0; i < 24; i++)
                    Marshal.WriteByte(variant, i, 0);
                Marshal.WriteInt16(variant, 0, (short)VT_BSTR);
                Marshal.WriteIntPtr(variant, 8, bstr);
                if (Com.Call<CreatePropertyConditionFn>(_automation, Slot.Automation_CreatePropertyCondition)(_automation, UIA_ClassNamePropertyId, variant, out _condition) < 0
                    || _condition == IntPtr.Zero)
                {
                    _failed = true;
                    return false;
                }
            }
            finally
            {
                Marshal.FreeBSTR(bstr);
                Marshal.FreeHGlobal(variant);
            }
            return true;
        }

        public void Dispose()
        {
            if (_condition != IntPtr.Zero)
                Marshal.Release(_condition);
            if (_automation != IntPtr.Zero)
                Marshal.Release(_automation);
            _condition = _automation = IntPtr.Zero;
        }

        private static class Native
        {
            [DllImport("ole32.dll")] public static extern int CoInitializeEx(IntPtr reserved, uint coInit);
            [DllImport("ole32.dll")] public static extern int CoCreateInstance(in Guid clsid, IntPtr outer, uint context, in Guid iid, out IntPtr obj);
        }
    }

    /// <summary>Pure pane bookkeeping: order, relative placement, matching across scans.</summary>
    internal static class PaneLayout
    {
        /// <summary>Left to right, then top to bottom (stable numbering for the sketch instances).</summary>
        public static IReadOnlyList<PaneInfo> Order(IEnumerable<PaneInfo> panes) =>
            panes.OrderBy(p => p.Bounds.Left).ThenBy(p => p.Bounds.Top).ToList();

        /// <summary>
        /// A pane as fractions of the window's content (the area all its panes
        /// share), plus how far that content sits from the window's edges.
        /// Windows Terminal splits the content by percentage, while the tab bar
        /// and borders keep their size in pixels: so while the window is being
        /// resized (or moved) the pane is placed exactly, not only at the next
        /// scan. <paramref name="content"/>: the union of the window's panes.
        /// </summary>
        public static PaneFraction ToFraction(Win32.RECT pane, Win32.RECT window, Win32.RECT content)
        {
            double w = Math.Max(1, content.Right - content.Left), h = Math.Max(1, content.Bottom - content.Top);
            return new PaneFraction((pane.Left - content.Left) / w, (pane.Top - content.Top) / h,
                                    (pane.Right - pane.Left) / w, (pane.Bottom - pane.Top) / h,
                                    content.Left - window.Left, content.Top - window.Top,
                                    window.Right - content.Right, window.Bottom - content.Bottom);
        }

        /// <summary>A pane alone in its window (the content is the pane).</summary>
        public static PaneFraction ToFraction(Win32.RECT pane, Win32.RECT window) => ToFraction(pane, window, pane);

        /// <summary>The pane back in screen pixels for the window's bounds now.</summary>
        public static Win32.RECT FromFraction(PaneFraction f, Win32.RECT window)
        {
            double cl = window.Left + f.InsetLeft, ct = window.Top + f.InsetTop;
            double w = Math.Max(1, window.Right - f.InsetRight - cl), h = Math.Max(1, window.Bottom - f.InsetBottom - ct);
            int left = (int)Math.Round(cl + f.X * w), top = (int)Math.Round(ct + f.Y * h);
            int right = (int)Math.Round(cl + (f.X + f.Width) * w), bottom = (int)Math.Round(ct + (f.Y + f.Height) * h);
            return new Win32.RECT { Left = left, Top = top, Right = right, Bottom = bottom };
        }

        /// <summary>A rectangle relative to its window's top-left corner.</summary>
        public static Win32.RECT Relative(Win32.RECT r, Win32.RECT window) => new()
        {
            Left = r.Left - window.Left, Top = r.Top - window.Top, Right = r.Right - window.Left, Bottom = r.Bottom - window.Top,
        };

        /// <summary>The smallest rectangle around all the panes (the window's content area).</summary>
        public static Win32.RECT Union(IReadOnlyList<PaneInfo> panes)
        {
            var u = panes[0].Bounds;
            foreach (var p in panes)
            {
                u.Left = Math.Min(u.Left, p.Bounds.Left);
                u.Top = Math.Min(u.Top, p.Bounds.Top);
                u.Right = Math.Max(u.Right, p.Bounds.Right);
                u.Bottom = Math.Max(u.Bottom, p.Bounds.Bottom);
            }
            return u;
        }

        /// <summary>
        /// Which old pane each new one is: greatest overlap first, so a sketch
        /// keeps its state when the panes around it change (a new split, a
        /// resized divider). Returns, per new pane, the index of the old one it
        /// continues, or -1 for a brand-new pane. Each old pane is used once.
        /// </summary>
        public static int[] Match(IReadOnlyList<Win32.RECT> old, IReadOnlyList<Win32.RECT> now)
        {
            var result = Enumerable.Repeat(-1, now.Count).ToArray();
            var pairs = new List<(long Overlap, int Old, int New)>();
            for (int o = 0; o < old.Count; o++)
                for (int n = 0; n < now.Count; n++)
                {
                    long overlap = Overlap(old[o], now[n]);
                    if (overlap > 0)
                        pairs.Add((overlap, o, n));
                }
            var usedOld = new HashSet<int>();
            foreach (var (_, o, n) in pairs.OrderByDescending(p => p.Overlap))
            {
                if (result[n] >= 0 || usedOld.Contains(o))
                    continue;
                result[n] = o;
                usedOld.Add(o);
            }
            return result;
        }

        private static long Overlap(Win32.RECT a, Win32.RECT b)
        {
            long w = Math.Min(a.Right, b.Right) - Math.Max(a.Left, b.Left);
            long h = Math.Min(a.Bottom, b.Bottom) - Math.Max(a.Top, b.Top);
            return w > 0 && h > 0 ? w * h : 0;
        }
    }

    /// <summary>Which panes get a sketch.</summary>
    internal static class PaneFilter
    {
        /// <summary>All of them, or with its own profile only the ones named after its tab title.</summary>
        public static IReadOnlyList<PaneInfo> ForProfile(IReadOnlyList<PaneInfo> panes, string? profileTitle) =>
            string.IsNullOrEmpty(profileTitle) ? panes : panes.Where(p => p.Name == profileTitle).ToList();
    }

    /// <summary>
    /// A pane's place: fractions (0-1) of its window's content area, and that
    /// area's distance from the window's edges in pixels (tab bar, borders).
    /// </summary>
    internal readonly record struct PaneFraction(double X, double Y, double Width, double Height,
                                                 int InsetLeft = 0, int InsetTop = 0, int InsetRight = 0, int InsetBottom = 0);

    /// <summary>
    /// When a scan must not change a window's panes. Windows Terminal's own
    /// menus and flyouts (the tab's context menu, the new-tab dropdown) are
    /// popups in front of the window, and while one is open UI Automation can
    /// report the panes off screen or not at all -- taken at face value, every
    /// sketch would hide and the see-through profile would show the desktop.
    /// So the layout is held while such a popup is in front, and when the
    /// panes vanish but the window's title (its active tab) didn't change.
    /// A real tab switch changes the title, or shows other panes.
    /// </summary>
    internal static class PaneHold
    {
        /// <summary>
        /// The mouse is over the window but not over its panes: on the tab bar,
        /// a border or the caption buttons. <paramref name="area"/>: where the
        /// panes are, relative to the window (from the last scan).
        /// </summary>
        public static bool CursorOverChrome((int X, int Y)? cursor, Win32.RECT window, Win32.RECT area)
        {
            if (cursor is not { } c)
                return false;
            bool inWindow = c.X >= window.Left && c.X < window.Right && c.Y >= window.Top && c.Y < window.Bottom;
            int x = c.X - window.Left, y = c.Y - window.Top;
            bool inPanes = x >= area.Left && x < area.Right && y >= area.Top && y < area.Bottom;
            return inWindow && !inPanes;
        }

        /// <param name="visiblePanes">How many panes UI Automation sees on screen (before filtering by profile).</param>
        /// <param name="hadPanes">The window had panes with sketches until now.</param>
        /// <param name="title">The window's title now.</param>
        /// <param name="lastTitle">Its title at the last scan that saw panes.</param>
        /// <param name="ownPopupInFront">One of the window's own popups (a menu) is the foreground window.</param>
        public static bool Hold(int visiblePanes, bool hadPanes, string? title, string? lastTitle, bool ownPopupInFront)
        {
            if (!hadPanes)
                return false;
            if (ownPopupInFront)
                return true;
            return visiblePanes == 0 && title == lastTitle;
        }
    }

    /// <summary>
    /// One pane, shared by the fleet's scanner (which measures it) and the host
    /// drawing its sketch (which reads it every frame). Thread-safe by swapping
    /// immutable values.
    /// </summary>
    internal sealed class PaneSlot
    {
        private volatile Box _box;

        private sealed record Box(PaneFraction Place, bool Visible, Win32.RECT LastBounds);
        private double _focusedAt;

        public PaneSlot(IntPtr window, PaneFraction place, Win32.RECT bounds)
        {
            Window = window;
            _box = new Box(place, true, bounds);
        }

        public IntPtr Window { get; }
        public PaneFraction Place => _box.Place;
        public bool Visible => _box.Visible;

        /// <summary>Where the scanner last saw it, relative to its window's top-left corner (for matching).</summary>
        public Win32.RECT LastBounds => _box.LastBounds;

        /// <summary>When it went out of sight (another tab, closed), for parking; NaN while visible.</summary>
        public double HiddenSince { get; set; } = double.NaN;

        /// <summary>
        /// When the scanner last saw it with the keyboard focus (scanner clock,
        /// seconds; 0 = never): the pane you type in, the one a shell signal is for.
        /// </summary>
        public double FocusedAt
        {
            get => Volatile.Read(ref _focusedAt);
            set => Volatile.Write(ref _focusedAt, value);
        }

        public void Update(PaneFraction place, Win32.RECT bounds) => _box = new Box(place, true, bounds);
        public void Hide() => _box = _box with { Visible = false };
    }
}
