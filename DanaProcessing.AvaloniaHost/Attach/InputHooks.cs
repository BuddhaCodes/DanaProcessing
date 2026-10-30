using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using DanaProcessing;

namespace DanaProcessing.AvaloniaHost.Attach
{
    /// <summary>Where the sketch's area is on screen: physical pixels, plus the monitor's scale.</summary>
    internal sealed record Area(int X, int Y, int Width, int Height, double Scale);

    /// <summary>Screen point → sketch coordinates. Pure, so it's testable without a desktop.</summary>
    internal static class InputGeometry
    {
        /// <summary>Local logical coordinates of a physical screen point (may fall outside the area).</summary>
        public static (float X, float Y) ToLocal(int screenX, int screenY, Area area) =>
            ((float)((screenX - area.X) / area.Scale), (float)((screenY - area.Y) / area.Scale));

        /// <summary>True (with local coordinates) when the point is inside the area.</summary>
        public static bool TryLocal(int screenX, int screenY, Area area, out float x, out float y)
        {
            (x, y) = ToLocal(screenX, screenY, area);
            return screenX >= area.X && screenY >= area.Y
                && screenX < area.X + area.Width && screenY < area.Y + area.Height;
        }
    }

    /// <summary>
    /// Windows virtual-key code → Processing's Key/KeyCode. Pure (the
    /// layout-dependent part comes in as <c>printable</c>), so it's testable.
    /// </summary>
    internal static class KeyMapping
    {
        public const int VK_BACK = 0x08, VK_TAB = 0x09, VK_RETURN = 0x0D, VK_SHIFT = 0x10, VK_CONTROL = 0x11,
                         VK_MENU = 0x12, VK_CAPITAL = 0x14, VK_ESCAPE = 0x1B, VK_LEFT = 0x25, VK_UP = 0x26,
                         VK_RIGHT = 0x27, VK_DOWN = 0x28, VK_DELETE = 0x2E, VK_LSHIFT = 0xA0, VK_RSHIFT = 0xA1,
                         VK_LCONTROL = 0xA2, VK_RCONTROL = 0xA3, VK_LMENU = 0xA4, VK_RMENU = 0xA5;

        /// <summary>
        /// Key and KeyCode for a key, the way the canvas host reports them:
        /// arrows and modifiers are CODED with UP/DOWN/LEFT/RIGHT/SHIFT/CONTROL/ALT;
        /// Enter, Backspace, Tab, Esc and Delete are their Processing chars;
        /// anything that types a character gets that character (layout and
        /// Shift respected) with KeyCode = the virtual-key code; the rest is
        /// CODED with KeyCode = the virtual-key code.
        /// </summary>
        public static (char Key, int KeyCode) Map(int vk, Func<int, char> printable) => vk switch
        {
            VK_UP => (Sketch.CODED, Sketch.UP),
            VK_DOWN => (Sketch.CODED, Sketch.DOWN),
            VK_LEFT => (Sketch.CODED, Sketch.LEFT),
            VK_RIGHT => (Sketch.CODED, Sketch.RIGHT),
            VK_SHIFT or VK_LSHIFT or VK_RSHIFT => (Sketch.CODED, Sketch.SHIFT),
            VK_CONTROL or VK_LCONTROL or VK_RCONTROL => (Sketch.CODED, Sketch.CONTROL),
            VK_MENU or VK_LMENU or VK_RMENU => (Sketch.CODED, Sketch.ALT),
            VK_RETURN => (Sketch.ENTER, 10),
            VK_BACK => (Sketch.BACKSPACE, 8),
            VK_TAB => (Sketch.TAB, 9),
            VK_ESCAPE => (Sketch.ESC, 27),
            VK_DELETE => (Sketch.DELETE, 127),
            _ => Printable(vk, printable),
        };

        private static (char, int) Printable(int vk, Func<int, char> printable)
        {
            char c = printable(vk);
            return c != '\0' && !char.IsControl(c) ? (c, vk) : (Sketch.CODED, vk);
        }
    }

    /// <summary>
    /// Low-level keyboard and/or mouse hooks for an attached sketch that opted
    /// in (ListenToTargetKeys / ListenToTargetMouse).
    ///
    /// The hooks live on their own thread, whose only job is a GetMessage
    /// loop: Windows waits for a low-level hook before delivering the input
    /// to anyone, so it must never sit behind a slow Draw(). The callbacks
    /// only filter and enqueue; the host drains <see cref="Queue"/> between
    /// frames. Nothing is swallowed: every event goes on to its window.
    ///
    /// Filtering, for privacy as much as correctness: keys only while the
    /// target is the foreground window; clicks only inside the sketch's area
    /// and only when the target (not a window on top of it) is under the
    /// cursor. A release is forwarded only if its press was.
    /// </summary>
    internal sealed class InputHooks : IDisposable
    {
        public ConcurrentQueue<TargetInput> Queue { get; } = new();
        public bool Keys { get; }
        public bool Mouse { get; }

        /// <summary>Why a hook couldn't be installed, or null.</summary>
        public string? Error { get; private set; }

        private readonly Thread _thread;
        private readonly Action? _enqueued;
        private readonly ManualResetEventSlim _ready = new();
        private uint _threadId;
        private IntPtr _keyboardHook, _mouseHook;
        private Win32.LowLevelProc? _keyboardProc, _mouseProc; // kept alive while hooked

        private volatile IntPtr _target;
        private volatile Area? _area;
        private int _forwardedButtons; // hook thread only

        /// <param name="enqueued">Called (on the hook thread) after each queued event: the host wakes up for it instead of waiting for its next frame.</param>
        public InputHooks(bool keys, bool mouse, Action? enqueued = null)
        {
            Keys = keys;
            Mouse = mouse;
            _enqueued = enqueued;
            _thread = new Thread(Run) { IsBackground = true, Name = "DanaProcessing attached sketch input" };
            _thread.Start();
            _ready.Wait(TimeSpan.FromSeconds(2));
        }

        /// <summary>Called by the host every frame: what to filter against. Zero/null while the target is hidden.</summary>
        public void Update(IntPtr target, Area? area)
        {
            _target = target;
            _area = area;
        }

        private void Run()
        {
            _threadId = Win32.GetCurrentThreadId();
            try
            {
                var module = Win32.GetModuleHandle(null);
                if (Keys)
                {
                    _keyboardProc = OnKeyboard;
                    _keyboardHook = Win32.SetWindowsHookEx(Win32.WH_KEYBOARD_LL, _keyboardProc, module, 0);
                    if (_keyboardHook == IntPtr.Zero)
                        Error = UiText.Tr($"No se pudo instalar el hook de teclado ({Marshal.GetLastWin32Error()}).", $"Couldn't install the keyboard hook ({Marshal.GetLastWin32Error()}).");
                }
                if (Mouse)
                {
                    _mouseProc = OnMouse;
                    _mouseHook = Win32.SetWindowsHookEx(Win32.WH_MOUSE_LL, _mouseProc, module, 0);
                    if (_mouseHook == IntPtr.Zero)
                        Error = UiText.Tr($"No se pudo instalar el hook de ratón ({Marshal.GetLastWin32Error()}).", $"Couldn't install the mouse hook ({Marshal.GetLastWin32Error()}).");
                }
            }
            finally
            {
                _ready.Set();
            }

            // Hook callbacks are delivered while this thread waits here.
            while (Win32.GetMessage(out var msg, IntPtr.Zero, 0, 0) > 0)
            {
                Win32.TranslateMessage(ref msg);
                Win32.DispatchMessage(ref msg);
            }

            if (_keyboardHook != IntPtr.Zero)
                Win32.UnhookWindowsHookEx(_keyboardHook);
            if (_mouseHook != IntPtr.Zero)
                Win32.UnhookWindowsHookEx(_mouseHook);
            _keyboardHook = _mouseHook = IntPtr.Zero;
        }

        private IntPtr OnKeyboard(int nCode, IntPtr wParam, IntPtr lParam)
        {
            try
            {
                if (nCode >= 0)
                {
                    long msg = wParam.ToInt64();
                    bool down = msg is 0x100 or 0x104;   // WM_KEYDOWN, WM_SYSKEYDOWN
                    bool up = msg is 0x101 or 0x105;     // WM_KEYUP, WM_SYSKEYUP
                    var target = _target;
                    if ((down || up) && target != IntPtr.Zero && Win32.GetForegroundWindow() == target)
                    {
                        var k = Marshal.PtrToStructure<Win32.KBDLLHOOKSTRUCT>(lParam);
                        var (key, code) = KeyMapping.Map((int)k.vkCode, vk => TypedChar(vk, k.scanCode, target));
                        Enqueue(new TargetInput(down ? TargetInputKind.KeyDown : TargetInputKind.KeyUp, key, code));
                    }
                }
            }
            catch
            {
                // Never let an exception escape into the system's input path.
            }
            return Win32.CallNextHookEx(_keyboardHook, nCode, wParam, lParam);
        }

        /// <summary>The character a key types in the target's keyboard layout, or '\0'.</summary>
        private static char TypedChar(int vk, uint scan, IntPtr target)
        {
            var state = new byte[256];
            if (Win32.GetAsyncKeyState(KeyMapping.VK_SHIFT) < 0)
                state[KeyMapping.VK_SHIFT] = 0x80;
            if ((Win32.GetKeyState(KeyMapping.VK_CAPITAL) & 1) != 0)
                state[KeyMapping.VK_CAPITAL] = 0x01;
            // Ctrl+Alt together is AltGr (@, €, [ on many layouts); Ctrl alone
            // would turn letters into control codes, so it's left out.
            if (Win32.GetAsyncKeyState(KeyMapping.VK_CONTROL) < 0 && Win32.GetAsyncKeyState(KeyMapping.VK_MENU) < 0)
            {
                state[KeyMapping.VK_CONTROL] = 0x80;
                state[KeyMapping.VK_MENU] = 0x80;
            }

            uint thread = Win32.GetWindowThreadProcessId(target, out _);
            var layout = Win32.GetKeyboardLayout(thread);
            var buffer = new char[4];
            // Flag 0x4: don't touch the keyboard state (keeps dead keys working in the target).
            int n = Win32.ToUnicodeEx((uint)vk, scan, state, buffer, buffer.Length, 0x4, layout);
            return n == 1 ? buffer[0] : '\0';
        }

        private IntPtr OnMouse(int nCode, IntPtr wParam, IntPtr lParam)
        {
            try
            {
                if (nCode >= 0)
                    HandleMouse((uint)wParam.ToInt64(), Marshal.PtrToStructure<Win32.MSLLHOOKSTRUCT>(lParam));
            }
            catch
            {
                // Never let an exception escape into the system's input path.
            }
            return Win32.CallNextHookEx(_mouseHook, nCode, wParam, lParam);
        }

        private void HandleMouse(uint msg, Win32.MSLLHOOKSTRUCT m)
        {
            var area = _area;
            var target = _target;

            (MouseButtonKind button, bool down) = msg switch
            {
                0x201 => (MouseButtonKind.Left, true),
                0x202 => (MouseButtonKind.Left, false),
                0x204 => (MouseButtonKind.Right, true),
                0x205 => (MouseButtonKind.Right, false),
                0x207 => (MouseButtonKind.Center, true),
                0x208 => (MouseButtonKind.Center, false),
                _ => (MouseButtonKind.None, false),
            };

            if (msg == 0x20A) // WM_MOUSEWHEEL: one notch = 120, positive = away from you
            {
                if (area != null && InputGeometry.TryLocal(m.pt.X, m.pt.Y, area, out _, out _) && IsOverTarget(m.pt, target))
                    Enqueue(new TargetInput(TargetInputKind.Wheel, Wheel: (short)(m.mouseData >> 16) / 120f));
                return;
            }
            if (button == MouseButtonKind.None)
                return;

            int bit = 1 << (int)button;
            if (down)
            {
                if (area != null && InputGeometry.TryLocal(m.pt.X, m.pt.Y, area, out float x, out float y) && IsOverTarget(m.pt, target))
                {
                    _forwardedButtons |= bit;
                    Enqueue(new TargetInput(TargetInputKind.MouseDown, Button: button, X: x, Y: y));
                }
            }
            else if ((_forwardedButtons & bit) != 0)
            {
                _forwardedButtons &= ~bit;
                var (x, y) = area != null ? InputGeometry.ToLocal(m.pt.X, m.pt.Y, area) : (0f, 0f);
                Enqueue(new TargetInput(TargetInputKind.MouseUp, Button: button, X: x, Y: y));
            }
        }

        /// <summary>Queues an event stamped with when the hook saw it, and wakes the host.</summary>
        private void Enqueue(TargetInput input)
        {
            Queue.Enqueue(input with { EnqueuedAt = Environment.TickCount64 });
            _enqueued?.Invoke();
        }

        /// <summary>The target itself is under the point, not a window on top of it (our overlay is click-through, so it's skipped).</summary>
        private static bool IsOverTarget(Win32.POINT p, IntPtr target)
        {
            if (target == IntPtr.Zero)
                return false;
            var hit = Win32.WindowFromPoint(p);
            return hit != IntPtr.Zero && Win32.GetAncestor(hit, Win32.GA_ROOT) == target;
        }

        public void Dispose()
        {
            if (_threadId != 0)
                Win32.PostThreadMessage(_threadId, Win32.WM_QUIT, IntPtr.Zero, IntPtr.Zero);
            _thread.Join(TimeSpan.FromSeconds(1));
            _ready.Dispose();
        }
    }
}
