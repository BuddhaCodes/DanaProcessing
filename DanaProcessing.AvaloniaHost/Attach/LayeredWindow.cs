using System.Runtime.InteropServices;
using SkiaSharp;

namespace DanaProcessing.AvaloniaHost.Attach
{
    /// <summary>
    /// What the attached-sketch host draws into and puts on screen, glued to
    /// its target: a CPU layered window (LayeredWindow, UpdateLayeredWindow)
    /// or a GPU one (DCompWindow, DirectComposition + a DXGI swap chain).
    /// </summary>
    internal interface IOverlayWindow : IDisposable
    {
        IntPtr Handle { get; }

        /// <summary>What the sketch draws into. Null until the first EnsureSize().</summary>
        SKSurface? Surface { get; }

        int PixelWidth { get; }
        int PixelHeight { get; }

        /// <summary>"CPU" or "GPU": how frames reach the screen (shown on the card).</summary>
        string Presenter { get; }

        /// <summary>Makes the surface width x height physical pixels; true when it was recreated (bind it to the sketch again).</summary>
        bool EnsureSize(int width, int height);

        /// <summary>Puts the pixels on screen at (x, y), in its Z-order slot next to the target. False if the presenter failed (the host then falls back to the CPU one).</summary>
        bool Present(int x, int y, IntPtr target, AttachLayer layer, bool pixelsChanged);

        /// <summary>Moves it without redrawing; true if it moved.</summary>
        bool MoveTo(int x, int y);

        void Hide();
    }

    /// <summary>
    /// The window part both presenters share: a borderless, click-through,
    /// never-activated popup, kept right above (or right below) its target.
    /// Must be created, used and disposed on one thread -- the one pumping
    /// its messages (AttachedSketchHost's).
    /// </summary>
    internal abstract class OverlayWindowBase
    {
        internal const string ClassName = "DanaProcessingAttachedSketch";

        // Kept alive for the life of the process: Windows calls it by pointer.
        private static readonly Win32.WndProc s_wndProc = WndProc;
        private static bool s_registered;
        private static readonly object s_registerLock = new();

        public IntPtr Handle { get; private set; }

        protected bool Shown;
        protected int X = int.MinValue, Y = int.MinValue;

        /// <param name="extraExStyle">Extra extended styles for this presenter.</param>
        /// <param name="layered">WS_EX_LAYERED: what makes WS_EX_TRANSPARENT let clicks through to other apps' windows.</param>
        protected OverlayWindowBase(int extraExStyle, bool layered = true)
        {
            var instance = Win32.GetModuleHandle(null);
            lock (s_registerLock)
            {
                if (!s_registered)
                {
                    var wc = new Win32.WNDCLASSEX
                    {
                        cbSize = Marshal.SizeOf<Win32.WNDCLASSEX>(),
                        lpfnWndProc = Marshal.GetFunctionPointerForDelegate(s_wndProc),
                        hInstance = instance,
                        lpszClassName = ClassName,
                    };
                    if (Win32.RegisterClassEx(ref wc) == 0 && Marshal.GetLastWin32Error() != 1410) // 1410: already registered
                        throw new InvalidOperationException(UiText.Tr($"RegisterClassEx falló ({Marshal.GetLastWin32Error()}).", $"RegisterClassEx failed ({Marshal.GetLastWin32Error()})."));
                    s_registered = true;
                }
            }

            int ex = (layered ? Win32.WS_EX_LAYERED : 0) | Win32.WS_EX_TRANSPARENT | Win32.WS_EX_TOOLWINDOW | Win32.WS_EX_NOACTIVATE | extraExStyle;
            Handle = Win32.CreateWindowEx(ex, ClassName, "DanaProcessing sketch", Win32.WS_POPUP,
                0, 0, 1, 1, IntPtr.Zero, IntPtr.Zero, instance, IntPtr.Zero);
            if (Handle == IntPtr.Zero)
                throw new InvalidOperationException(UiText.Tr($"CreateWindowEx falló ({Marshal.GetLastWin32Error()}).", $"CreateWindowEx failed ({Marshal.GetLastWin32Error()})."));
        }

        private static IntPtr WndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
        {
            // Clicks fall through to whatever is under us; never take focus.
            if (msg == Win32.WM_NCHITTEST)
                return Win32.HTTRANSPARENT;
            if (msg == Win32.WM_MOUSEACTIVATE)
                return Win32.MA_NOACTIVATE;
            return Win32.DefWindowProc(hWnd, msg, wParam, lParam);
        }

        protected void KeepZOrder(IntPtr target, AttachLayer layer)
        {
            const uint flags = Win32.SWP_NOMOVE | Win32.SWP_NOSIZE | Win32.SWP_NOACTIVATE | Win32.SWP_NOOWNERZORDER;
            // Windows that don't count between us and the target: hidden ones
            // (a terminal's closed tooltips) and the other sketch overlays --
            // with one sketch per pane, every pane's overlay wants the same spot
            // next to the terminal, and each moving itself back there every
            // frame made them swap places forever (and the screen flicker).
            Func<IntPtr, bool> skip = h => h != Handle && (!Win32.IsWindowVisible(h) || IsSketchOverlay(h));

            if (layer == AttachLayer.Behind)
            {
                // "Insert after" the target = directly below it.
                if (!ZOrder.Reaches(Handle, target, h => Win32.GetWindow(h, Win32.GW_HWNDPREV), skip))
                {
                    AttachTrace.Write("zorder", $"{AttachTrace.Describe(Handle)} below {AttachTrace.Describe(target)} (was below {AttachTrace.Describe(Win32.GetWindow(Handle, Win32.GW_HWNDPREV))})");
                    Win32.SetWindowPos(Handle, target, 0, 0, 0, 0, flags);
                }
                return;
            }

            // Above: directly below whatever is directly above the target.
            if (ZOrder.Reaches(target, Handle, h => Win32.GetWindow(h, Win32.GW_HWNDPREV), skip))
                return;
            var above = Win32.GetWindow(target, Win32.GW_HWNDPREV);
            // Inserting after a topmost window would make us topmost too (on
            // top of everything, forever): go to the top of the normal band
            // instead, which is still right above the target in that case.
            bool aboveIsTopmost = above != IntPtr.Zero &&
                (Win32.GetWindowLongPtr(above, Win32.GWL_EXSTYLE).ToInt64() & Win32.WS_EX_TOPMOST) != 0;
            AttachTrace.Write("zorder", $"{AttachTrace.Describe(Handle)} above {AttachTrace.Describe(target)} (under {AttachTrace.Describe(above)})");
            Win32.SetWindowPos(Handle, above == IntPtr.Zero || aboveIsTopmost ? Win32.HWND_TOP : above, 0, 0, 0, 0, flags);
        }

        private static bool IsSketchOverlay(IntPtr hwnd)
        {
            var sb = new System.Text.StringBuilder(64);
            Win32.GetClassName(hwnd, sb, sb.Capacity);
            return sb.ToString() == ClassName;
        }

        /// <summary>
        /// Moves the overlay without redrawing it (the target moved between
        /// frames). Returns true if it moved. Only once it's on screen.
        /// </summary>
        public bool MoveTo(int x, int y)
        {
            if (!Shown || (x == X && y == Y))
                return false;
            Win32.SetWindowPos(Handle, IntPtr.Zero, x, y, 0, 0,
                Win32.SWP_NOSIZE | Win32.SWP_NOZORDER | Win32.SWP_NOACTIVATE | Win32.SWP_NOOWNERZORDER);
            X = x;
            Y = y;
            return true;
        }

        public void Hide()
        {
            if (!Shown)
                return;
            Win32.ShowWindow(Handle, Win32.SW_HIDE);
            Shown = false;
            AttachTrace.Write("overlay", $"{AttachTrace.Describe(Handle)} hidden");
        }

        protected void ShowIfHidden()
        {
            if (Shown)
                return;
            Win32.ShowWindow(Handle, Win32.SW_SHOWNOACTIVATE);
            Shown = true;
            AttachTrace.Write("overlay", $"{AttachTrace.Describe(Handle)} shown at ({X},{Y})");
        }

        protected void DestroyWindow()
        {
            if (Handle != IntPtr.Zero)
            {
                Win32.DestroyWindow(Handle);
                Handle = IntPtr.Zero;
            }
            Shown = false;
        }
    }

    /// <summary>
    /// The CPU presenter: a layered window whose pixels come straight from a
    /// Skia surface. The surface draws into a top-down DIB (premultiplied
    /// BGRA, exactly what UpdateLayeredWindow wants), so a frame goes to
    /// screen with no conversion -- but every frame UpdateLayeredWindow copies
    /// the whole bitmap to DWM through GDI.
    /// </summary>
    internal sealed class LayeredWindow : OverlayWindowBase, IOverlayWindow
    {
        public SKSurface? Surface { get; private set; }

        public int PixelWidth { get; private set; }
        public int PixelHeight { get; private set; }

        public string Presenter => "CPU";

        private IntPtr _memDC, _dib, _oldBmp, _bits;

        public LayeredWindow() : base(0)
        {
        }

        /// <summary>
        /// Makes the surface exactly width x height physical pixels. Returns
        /// true when it had to be recreated -- the caller must bind the new
        /// Surface to the sketch (the old pixels are gone).
        /// </summary>
        public bool EnsureSize(int width, int height)
        {
            width = Math.Max(1, width);
            height = Math.Max(1, height);
            if (Surface != null && width == PixelWidth && height == PixelHeight)
                return false;

            FreeSurface();

            var screen = Win32.GetDC(IntPtr.Zero);
            _memDC = Win32.CreateCompatibleDC(screen);
            Win32.ReleaseDC(IntPtr.Zero, screen);

            var bi = new Win32.BITMAPINFOHEADER
            {
                biSize = Marshal.SizeOf<Win32.BITMAPINFOHEADER>(),
                biWidth = width,
                biHeight = -height, // top-down, like Skia
                biPlanes = 1,
                biBitCount = 32,
            };
            _dib = Win32.CreateDIBSection(_memDC, ref bi, 0, out _bits, IntPtr.Zero, 0);
            if (_dib == IntPtr.Zero || _bits == IntPtr.Zero)
                throw new InvalidOperationException(UiText.Tr($"No se pudo crear el bitmap de {width}x{height} para el sketch adjunto.", $"Couldn't create the {width}x{height} bitmap for the attached sketch."));
            _oldBmp = Win32.SelectObject(_memDC, _dib);

            var info = new SKImageInfo(width, height, SKColorType.Bgra8888, SKAlphaType.Premul);
            Surface = SKSurface.Create(info, _bits, width * 4)
                ?? throw new InvalidOperationException(UiText.Tr("SKSurface.Create devolvió null para el bitmap del sketch adjunto.", "SKSurface.Create returned null for the attached sketch's bitmap."));
            Surface.Canvas.Clear(SKColors.Transparent);

            PixelWidth = width;
            PixelHeight = height;
            return true;
        }

        /// <summary>
        /// Puts the current pixels on screen at (x, y) and keeps the window in
        /// its slot in the Z order: right above the target, or right below it.
        /// Pass <paramref name="pixelsChanged"/> = false when nothing was drawn
        /// since the last call -- then only a move costs anything.
        /// </summary>
        public bool Present(int x, int y, IntPtr target, AttachLayer layer, bool pixelsChanged)
        {
            if (Surface == null)
                return true;

            bool moved = x != X || y != Y;
            if (pixelsChanged || moved || !Shown)
            {
                Surface.Canvas.Flush();
                Win32.GdiFlush();

                var dst = new Win32.POINT { X = x, Y = y };
                var src = new Win32.POINT();
                var size = new Win32.SIZE { CX = PixelWidth, CY = PixelHeight };
                var blend = new Win32.BLENDFUNCTION { BlendOp = 0, SourceConstantAlpha = 255, AlphaFormat = 1 }; // AC_SRC_ALPHA
                Win32.UpdateLayeredWindow(Handle, IntPtr.Zero, ref dst, ref size, _memDC, ref src, 0, ref blend, Win32.ULW_ALPHA);
                X = x;
                Y = y;
            }

            KeepZOrder(target, layer);
            ShowIfHidden();
            return true;
        }

        private void FreeSurface()
        {
            Surface?.Dispose();
            Surface = null;
            if (_memDC != IntPtr.Zero)
            {
                if (_oldBmp != IntPtr.Zero)
                    Win32.SelectObject(_memDC, _oldBmp);
                Win32.DeleteDC(_memDC);
            }
            if (_dib != IntPtr.Zero)
                Win32.DeleteObject(_dib);
            _memDC = _dib = _oldBmp = _bits = IntPtr.Zero;
            PixelWidth = PixelHeight = 0;
        }

        public void Dispose()
        {
            FreeSurface();
            DestroyWindow();
        }
    }
}

namespace DanaProcessing.AvaloniaHost.Attach
{
    /// <summary>Z-order bookkeeping, pure so it can be tested with a fake window stack.</summary>
    internal static class ZOrder
    {
        /// <summary>
        /// Walking up the z-order from <paramref name="from"/> (with
        /// <paramref name="prev"/> = GW_HWNDPREV), is <paramref name="to"/> the
        /// first window that isn't skipped? That is: nothing that counts sits
        /// between them. Gives up (false) after 64 windows.
        /// </summary>
        public static bool Reaches(IntPtr from, IntPtr to, Func<IntPtr, IntPtr> prev, Func<IntPtr, bool> skip)
        {
            var w = prev(from);
            for (int i = 0; i < 64 && w != IntPtr.Zero; i++, w = prev(w))
            {
                if (w == to)
                    return true;
                if (!skip(w))
                    return false;
            }
            return false;
        }
    }
}
